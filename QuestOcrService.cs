using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ScrcpyLauncher
{
    public class QuestEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }     // 포획, 토벌, 사냥, 부위파괴, 채집 등
        public string Region { get; set; }       // 거목의 벌판, 열사 평원, 부패의 심연, 찬란한 군도, 둥지, 전 지역(필드)
        public bool IsNest { get; set; }         // 둥지 (인스턴스)
        public bool IsAnyField { get; set; }     // 아무 필드에서나 가능 (원거리/근거리 사냥 등)
        public string Species { get; set; }      // 아룡종, 조룡종, 어룡종, 수룡종, 비룡종, 고룡종, 융광종, (없음)
        public string TargetMonster { get; set; } // 둥지 등 특정 몬스터 이름 (예: 치치야크, 안쟈나프)
        public string ActionType { get; set; }   // "포획", "토벌", "사냥", "기타"
        public string WeaponType { get; set; }   // "원거리", "근거리", ""
        public string ObjectiveText { get; set; }
        public string Progress { get; set; }
        public string RawFingerprint { get; set; }

        public string SummaryLine
        {
            get
            {
                var tags = new List<string>();
                if (!string.IsNullOrEmpty(Region)) tags.Add(Region);
                if (!string.IsNullOrEmpty(Species)) tags.Add(Species);
                if (!string.IsNullOrEmpty(ActionType) && ActionType != "기타") tags.Add(ActionType);
                if (!string.IsNullOrEmpty(WeaponType)) tags.Add(WeaponType + " 무기");
                string tagStr = tags.Count > 0 ? "[" + string.Join(" · ", tags.ToArray()) + "] " : "";
                string prog = string.IsNullOrEmpty(Progress) ? "" : (" (" + Progress + ")");
                return string.Format("{0}{1}{2} — {3}", tagStr, Title, prog, ObjectiveText);
            }
        }

        public override string ToString()
        {
            return SummaryLine;
        }
    }

    public class QuestPlanGroup
    {
        public string Header { get; set; }
        public string RegionTitle { get; set; }
        public string SpeciesTitle { get; set; }
        public string ActionBadge { get; set; }
        public string WeaponBadge { get; set; }
        public string TargetMonsters { get; set; }
        public string Tip { get; set; }
        public bool IsNestGroup { get; set; }
        public List<QuestEntry> Quests { get; set; }
        public int Count { get { return Quests != null ? Quests.Count : 0; } }

        public QuestPlanGroup()
        {
            Quests = new List<QuestEntry>();
        }
    }

    public static class QuestOcrService
    {
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public static readonly string[] FieldRegions = new[]
        {
            "거목의 벌판",
            "열사 평원",
            "부패의 심연",
            "찬란한 군도"
        };

        public static readonly string[] AllSpecies = new[]
        {
            "아룡종",
            "조룡종",
            "어룡종",
            "수룡종",
            "비룡종",
            "고룡종",
            "융광종"
        };

        // Sorted longest-first so variants (아종/융광종) match before base monster names
        public static readonly string[] AllMonsterNames = new[]
        {
            "융광종 도스쟈그라스", "융광종 도스기르오스", "융광종 쿠루루야크", "융광종 쥬라토도스",
            "융광종 토비카가치", "융광종 파오우르무", "융광종 레이기에나", "융광종 리오레이아",
            "융광종 리오레우스", "융광종 디아블로스", "융광종 푸케푸케", "융광종 볼보로스",
            "융광종 안쟈나프", "융광종 치치야크", "융광종 오도가론",
            "리오레이아 아종", "디아블로스 아종", "루가 에알다",
            "도스쟈그라스", "도스기르오스", "쿠루루야크", "쥬라토도스",
            "토비카가치", "파오우르무", "레이기에나", "리오레이아",
            "리오레우스", "디아블로스", "라도발킨", "크샬다오라",
            "푸케푸케", "볼보로스", "안쟈나프", "치치야크", "오도가론"
        };

        public static readonly Dictionary<string, string> SpeciesMonstersMap = new Dictionary<string, string>
        {
            { "아룡종", "도스쟈그라스, 도스기르오스, 토비카가치, 오도가론" },
            { "조룡종", "쿠루루야크, 푸케푸케, 치치야크" },
            { "어룡종", "쥬라토도스" },
            { "수룡종", "볼보로스, 안쟈나프, 라도발킨" },
            { "비룡종", "리오레이아, 리오레이아 아종, 리오레우스, 디아블로스, 디아블로스 아종, 파오우르무, 레이기에나" },
            { "고룡종", "루가 에알다, 크샬다오라" },
            { "융광종", "" } // 모든 몬스터의 변이 개체이므로 목록 생략
        };

        public static readonly Dictionary<string, string> RadiantSpeciesMonstersMap = new Dictionary<string, string>
        {
            { "아룡종", "융광종 도스쟈그라스, 융광종 도스기르오스, 융광종 토비카가치, 융광종 오도가론" },
            { "조룡종", "융광종 쿠루루야크, 융광종 푸케푸케, 융광종 치치야크" },
            { "어룡종", "융광종 쥬라토도스" },
            { "수룡종", "융광종 볼보로스, 융광종 안쟈나프" },
            { "비룡종", "융광종 리오레이아, 융광종 리오레우스, 융광종 디아블로스, 융광종 파오우르무, 융광종 레이기에나" }
        };

        public static string GetSpeciesMonsters(string species, bool radiantOnly = false)
        {
            if (string.IsNullOrEmpty(species)) return "";
            string val;
            if (radiantOnly)
                return RadiantSpeciesMonstersMap.TryGetValue(species, out val) ? val : "";
            return SpeciesMonstersMap.TryGetValue(species, out val) ? val : "";
        }

        public static string GetSpeciesGuideText()
        {
            return
                "[ 몬스터헌터 아웃랜더스 종족별 몬스터 & 약점 ]\n\n" +
                "• 아룡종 : 도스쟈그라스(불), 도스기르오스(물), 토비카가치(물), 오도가론(얼음)\n\n" +
                "• 조룡종 : 쿠루루야크(물), 푸케푸케(번개), 치치야크(얼음/번개)\n\n" +
                "• 어룡종 : 쥬라토도스(물/번개)\n\n" +
                "• 수룡종 : 볼보로스(불), 안쟈나프(물), 라도발킨(용)\n\n" +
                "• 비룡종 : 리오레이아(용), 리오레이아 아종(용), 리오레우스(용), 디아블로스(얼음), 디아블로스 아종, 파오우르무(불), 레이기에나(번개)\n\n" +
                "• 고룡종 : 루가 에알다(불/얼음), 크샬다오라\n\n" +
                "• 융광종 : 기존 종족 몬스터의 변이 개체 (포획 불가 · 해당 종족 사냥/토벌 퀘스트와 동시 진행 가능!)";
        }

        public static async Task<Tuple<QuestEntry, string>> ScanQuestDetailFromWindowAsync(IntPtr targetHwnd, int videoW, int videoH)
        {
            if (targetHwnd == IntPtr.Zero || IsIconic(targetHwnd))
                return Tuple.Create<QuestEntry, string>(null, "미러링 창이 열려 있지 않습니다.");

            Bitmap cropBmp = null;
            try
            {
                cropBmp = CaptureQuestDetailCrop(targetHwnd, videoW, videoH);
                if (cropBmp == null)
                    return Tuple.Create<QuestEntry, string>(null, "화면 캡처에 실패했습니다.");

                string[] lines = await RunWinRtOcrAsync(cropBmp);
                if (lines == null || lines.Length == 0)
                    return Tuple.Create<QuestEntry, string>(null, "퀘스트 텍스트를 인식하지 못했습니다.");

                QuestEntry entry = ParseQuestLines(lines);
                if (entry == null)
                    return Tuple.Create<QuestEntry, string>(null, "퀘스트 상세 화면이 아닌 것 같습니다.");

                return Tuple.Create(entry, "OK");
            }
            catch (Exception ex)
            {
                return Tuple.Create<QuestEntry, string>(null, "OCR 오류: " + ex.Message);
            }
            finally
            {
                if (cropBmp != null) cropBmp.Dispose();
            }
        }

        private static Bitmap CaptureQuestDetailCrop(IntPtr hwnd, int videoW, int videoH)
        {
            RECT rc;
            if (!GetClientRect(hwnd, out rc)) return null;
            int cw = rc.Right - rc.Left;
            int ch = rc.Bottom - rc.Top;
            if (cw < 200 || ch < 120) return null;

            using (var full = new Bitmap(cw, ch, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(full))
                {
                    IntPtr hdc = g.GetHdc();
                    try { PrintWindow(hwnd, hdc, 3); }
                    finally { g.ReleaseHdc(hdc); }
                }

                // Compute actual centered video rect inside client rect (handles 21:9 letterbox!)
                double vx = 0, vy = 0, vw = cw, vh = ch;
                if (videoW > 0 && videoH > 0)
                {
                    double winAspect = (double)cw / ch;
                    double vidAspect = (double)videoW / videoH;
                    if (winAspect > vidAspect + 0.002)
                    {
                        vh = ch;
                        vw = ch * vidAspect;
                        vx = (cw - vw) / 2.0;
                    }
                    else if (winAspect < vidAspect - 0.002)
                    {
                        vw = cw;
                        vh = cw / vidAspect;
                        vy = (ch - vh) / 2.0;
                    }
                }

                // Right quest detail panel: Title ribbon + Region + Objective ONLY (x: 48%..97%, y: 15.5%..34.5%)
                // Skipping y < 15.5% completely avoids the top category badge (포획/필드) being misread as the quest title!
                int rx = Math.Max(0, (int)Math.Round(vx + vw * 0.48));
                int ry = Math.Max(0, (int)Math.Round(vy + vh * 0.155));
                int rw = Math.Min(cw - rx, (int)Math.Round(vw * 0.49));
                int rh = Math.Min(ch - ry, (int)Math.Round(vh * 0.19));
                if (rw < 80 || rh < 40) return null;

                // 2x HighQualityBicubic upscale + Blue-channel contrast enhancement so orange highlight text pops out pitch black!
                var scaled = new Bitmap(rw * 2, rh * 2, PixelFormat.Format32bppArgb);
                using (var sg = Graphics.FromImage(scaled))
                {
                    sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    sg.DrawImage(full, new Rectangle(0, 0, scaled.Width, scaled.Height), new Rectangle(rx, ry, rw, rh), GraphicsUnit.Pixel);
                }
                EnhanceParchmentContrast(scaled);
                return scaled;
            }
        }

        private static void EnhanceParchmentContrast(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int titleSplitY = (int)(h * 0.38); // top ~38% of crop is the dark brown title ribbon with light text
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * h;
                byte[] buf = new byte[bytes];
                Marshal.Copy(data.Scan0, buf, 0, bytes);

                for (int y = titleSplitY; y < h; y++)
                {
                    int row = y * data.Stride;
                    for (int x = 0; x < w; x++)
                    {
                        int idx = row + x * 4;
                        byte b = buf[idx];
                        byte g = buf[idx + 1];
                        byte r = buf[idx + 2];

                        // Light beige parchment + dark brown & ORANGE highlight text (orange has very low Blue!)
                        int bWeighted = (r * 1 + g * 2 + b * 7) / 10;
                        int stretched = (bWeighted - 65) * 255 / 105;
                        byte val = (byte)Math.Max(0, Math.Min(255, stretched));

                        buf[idx] = val;
                        buf[idx + 1] = val;
                        buf[idx + 2] = val;
                        buf[idx + 3] = 255;
                    }
                }
                Marshal.Copy(buf, 0, data.Scan0, bytes);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static Task<T> AwaitOp<T>(Windows.Foundation.IAsyncOperation<T> op)
        {
            var tcs = new TaskCompletionSource<T>();
            op.Completed = (info, status) =>
            {
                if (status == Windows.Foundation.AsyncStatus.Completed)
                    tcs.TrySetResult(info.GetResults());
                else if (status == Windows.Foundation.AsyncStatus.Canceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetException(info.ErrorCode ?? new Exception("WinRT async error"));
            };
            return tcs.Task;
        }

        private static async Task<string[]> RunWinRtOcrAsync(Bitmap bmp)
        {
            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngBytes = ms.ToArray();
            }

            using (var ras = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(pngBytes);
                    await AwaitOp<uint>(writer.StoreAsync());
                    await AwaitOp<bool>(writer.FlushAsync());
                    writer.DetachStream();
                }

                ras.Seek(0);
                var decoder = await AwaitOp<BitmapDecoder>(BitmapDecoder.CreateAsync(ras));
                using (var sbmp = await AwaitOp<SoftwareBitmap>(decoder.GetSoftwareBitmapAsync()))
                {
                    OcrEngine engine = null;
                    try
                    {
                        var koLang = new Windows.Globalization.Language("ko");
                        if (OcrEngine.IsLanguageSupported(koLang))
                            engine = OcrEngine.TryCreateFromLanguage(koLang);
                    }
                    catch { }
                    if (engine == null)
                        engine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (engine == null)
                        throw new Exception("Windows OCR 언어 팩을 찾을 수 없습니다.");

                    var res = await AwaitOp<OcrResult>(engine.RecognizeAsync(sbmp));
                    var list = new List<string>();
                    foreach (var line in res.Lines)
                    {
                        string t = (line.Text ?? "").Trim();
                        if (!string.IsNullOrEmpty(t)) list.Add(t);
                    }
                    return list.ToArray();
                }
            }
        }

        public static QuestEntry ParseQuestLines(string[] lines)
        {
            if (lines == null || lines.Length < 2) return null;

            string allText = string.Join(" ", lines);

            // Extract progress like 0/1, 1/3, 0/5
            string progress = "";
            var pm = Regex.Match(allText, @"\b(\d+\s*/\s*\d+)\b");
            if (pm.Success) progress = pm.Groups[1].Value.Replace(" ", "");

            // Detect objective line (longest descriptive line or line containing 에서/포획/토벌/사냥/파괴/무기/둥지 or species)
            string objective = "";
            int objIdx = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = CleanLeadingBullet(lines[i]);
                if (Regex.IsMatch(l, @"^\d+\s*/\s*\d+$")) continue;
                if (l.Contains("에서") || l.Contains("포획") || l.Contains("토벌") || l.Contains("사냥") ||
                    l.Contains("파괴") || l.Contains("원거리") || l.Contains("근거리") || l.Contains("둥지") ||
                    FindBestMatch(l, AllSpecies, 1) != "")
                {
                    if (l.Length >= objective.Length)
                    {
                        objective = l;
                        objIdx = i;
                    }
                }
            }

            if (string.IsNullOrEmpty(objective))
                return null;

            // Remove trailing progress from objective
            objective = Regex.Replace(objective, @"\s*\d+\s*/\s*\d+\s*$", "").Trim();

            // Detect quest title: usually line 1 (right below category badge like "포획"), not equal to region or objective
            string title = "";
            string category = "";
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == objIdx) continue;
                string l = CleanLeadingBullet(lines[i]);
                if (Regex.IsMatch(l, @"^\d+\s*/\s*\d+$")) continue;
                if (l.Length <= 4 && (l.Contains("포획") || l.Contains("토벌") || l.Contains("사냥") || l.Contains("수렵") || l.Contains("의뢰") || l.Contains("긴급")))
                {
                    category = l;
                    continue;
                }
                if (FindBestMatch(l, FieldRegions, 1) != "" && l.Length <= 8) continue;
                if (l.Contains("둥지") && l.Length <= 6) continue;
                if (string.IsNullOrEmpty(title) && l.Length >= 2)
                {
                    title = l;
                }
            }
            if (string.IsNullOrEmpty(title)) title = objective;

            // Detect Region (Field 4 + 둥지 + 전 지역)
            string region = "";
            bool isNest = false;
            bool isAnyField = false;

            if (allText.Contains("둥지"))
            {
                region = "둥지";
                isNest = true;
            }
            else
            {
                region = FindBestMatch(allText, FieldRegions, 1);
            }

            // Detect Weapon condition (원거리 / 근거리)
            string weapon = "";
            if (allText.Contains("원거리")) weapon = "원거리";
            else if (allText.Contains("근거리")) weapon = "근거리";

            if (string.IsNullOrEmpty(region))
            {
                if (allText.Contains("필드") || !string.IsNullOrEmpty(weapon))
                {
                    region = "필드 (지역 무관)";
                    isAnyField = true;
                }
                else
                {
                    region = "기타/미분류";
                    isAnyField = true;
                }
            }

            // Detect Monster Species & specific Monster Name (for Nest quests!)
            string species = FindBestMatch(allText, AllSpecies, 1);
            string targetMonster = FindBestMatch(objective, AllMonsterNames, 1);
            if (!string.IsNullOrEmpty(targetMonster))
                objective = ReplaceFuzzySubstring(objective, targetMonster, 1);

            // Detect Action Type (포획 / 토벌 / 사냥 / 기타)
            string action = "기타";
            if (objective.Contains("포획")) action = "포획";
            else if (objective.Contains("토벌")) action = "토벌";
            else if (objective.Contains("사냥") || objective.Contains("수렵")) action = "사냥";
            else if (category.Contains("포획")) action = "포획";
            else if (category.Contains("토벌")) action = "토벌";
            else if (category.Contains("사냥") || category.Contains("수렵")) action = "사냥";

            // Fix 1-char OCR typo of region name inside objective text for clean display
            if (!isNest && !isAnyField && !string.IsNullOrEmpty(region))
                objective = ReplaceFuzzySubstring(objective, region, 1);
            if (!string.IsNullOrEmpty(species))
                objective = ReplaceFuzzySubstring(objective, species, 1);

            string fp = (title + "|" + region + "|" + species + "|" + action + "|" + weapon + "|" + objective).ToLowerInvariant();

            return new QuestEntry
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Title = title,
                Category = category,
                Region = region,
                IsNest = isNest,
                IsAnyField = isAnyField,
                Species = species,
                TargetMonster = targetMonster,
                ActionType = action,
                WeaponType = weapon,
                ObjectiveText = objective,
                Progress = progress,
                RawFingerprint = fp
            };
        }

        public static List<QuestPlanGroup> BuildSimultaneousPlans(List<QuestEntry> quests)
        {
            var result = new List<QuestPlanGroup>();
            if (quests == null || quests.Count == 0) return result;

            var anyFieldQuests = quests.Where(q => q.IsAnyField && !q.IsNest).ToList();
            var nestQuests = quests.Where(q => q.IsNest).ToList();
            var fieldQuests = quests.Where(q => !q.IsAnyField && !q.IsNest).ToList();

            var allGroups = new List<QuestPlanGroup>();

            // 1. Nest quests: group strictly by TargetMonster so different Nest monsters NEVER stack as simultaneous clears!
            if (nestQuests.Count > 0)
            {
                var nestByMonster = new Dictionary<string, List<QuestEntry>>();
                foreach (var nq in nestQuests)
                {
                    string monKey = !string.IsNullOrEmpty(nq.TargetMonster) ? nq.TargetMonster : (nq.ObjectiveText ?? nq.Id);
                    List<QuestEntry> nb;
                    if (!nestByMonster.TryGetValue(monKey, out nb))
                    {
                        nb = new List<QuestEntry>();
                        nestByMonster[monKey] = nb;
                    }
                    nb.Add(nq);
                }

                foreach (var kv in nestByMonster)
                {
                    var items = kv.Value;
                    string monName = !string.IsNullOrEmpty(items[0].TargetMonster) ? items[0].TargetMonster : "인스턴스";
                    allGroups.Add(new QuestPlanGroup
                    {
                        Header = string.Format("[둥지 - {0}]  ★사냥  — {1}개", monName, items.Count),
                        RegionTitle = "둥지",
                        SpeciesTitle = monName,
                        ActionBadge = "★사냥",
                        WeaponBadge = "",
                        TargetMonsters = "",
                        Tip = "",
                        IsNestGroup = true,
                        Quests = items
                    });
                }
            }

            // Separate Radiant (융광종) field quests by region — Radiant is a variant modifier on ANY existing species!
            var radiantByRegion = new Dictionary<string, List<QuestEntry>>();
            var normalFieldQuests = new List<QuestEntry>();
            foreach (var q in fieldQuests)
            {
                if (q.Species == "융광종")
                {
                    List<QuestEntry> rb;
                    if (!radiantByRegion.TryGetValue(q.Region, out rb))
                    {
                        rb = new List<QuestEntry>();
                        radiantByRegion[q.Region] = rb;
                    }
                    rb.Add(q);
                }
                else
                {
                    normalFieldQuests.Add(q);
                }
            }

            // 2. Group regular-species field quests by (Region + Species)
            var byRegionSpecies = new Dictionary<string, List<QuestEntry>>();
            foreach (var q in normalFieldQuests)
            {
                string sp = string.IsNullOrEmpty(q.Species) ? "공통" : q.Species;
                string key = q.Region + "||" + sp;
                List<QuestEntry> bucket;
                if (!byRegionSpecies.TryGetValue(key, out bucket))
                {
                    bucket = new List<QuestEntry>();
                    byRegionSpecies[key] = bucket;
                }
                bucket.Add(q);
            }

            var usedRadiantIds = new HashSet<string>();
            var usedAnyFieldIds = new HashSet<string>();

            foreach (var kv in byRegionSpecies)
            {
                var list = kv.Value;
                string region = list[0].Region;
                string rawSp = list[0].Species ?? "";
                string spLabel = string.IsNullOrEmpty(rawSp) ? "일반" : rawSp;
                string monsters = GetSpeciesMonsters(rawSp, false);
                string radiantMonsters = GetSpeciesMonsters(rawSp, true);

                var capList = list.Where(x => x.ActionType == "포획").ToList();
                var slayList = list.Where(x => x.ActionType == "토벌").ToList();
                var huntList = list.Where(x => x.ActionType != "포획" && x.ActionType != "토벌").ToList();

                var bonus = new List<QuestEntry>();
                foreach (var af in anyFieldQuests)
                {
                    if (string.IsNullOrEmpty(af.Species) || string.IsNullOrEmpty(rawSp) || af.Species == rawSp)
                        bonus.Add(af);
                }

                List<QuestEntry> regionRadiants;
                radiantByRegion.TryGetValue(region, out regionRadiants);
                bool hasNonCapture = (slayList.Count > 0 || huntList.Count > 0);
                bool canStackRadiant = (hasNonCapture && regionRadiants != null && regionRadiants.Count > 0 && rawSp != "고룡종");

                if (capList.Count > 0 && (slayList.Count > 0 || canStackRadiant))
                {
                    var capCombined = new List<QuestEntry>();
                    capCombined.AddRange(capList);
                    capCombined.AddRange(huntList);
                    capCombined.AddRange(bonus);
                    AddFieldGroupsSplitByWeapon(allGroups, region, spLabel, "★포획", monsters, capCombined);

                    var nonCap = new List<QuestEntry>();
                    nonCap.AddRange(slayList);
                    nonCap.AddRange(huntList);
                    if (canStackRadiant)
                    {
                        nonCap.AddRange(regionRadiants);
                        foreach (var rq in regionRadiants) usedRadiantIds.Add(rq.Id);
                    }
                    nonCap.AddRange(bonus);
                    if (nonCap.Count > bonus.Count)
                    {
                        string lbl = canStackRadiant ? ("융광종(" + spLabel + ")") : spLabel;
                        string act = slayList.Count > 0 ? "★토벌" : "★사냥";
                        AddFieldGroupsSplitByWeapon(allGroups, region, lbl, act, canStackRadiant ? radiantMonsters : monsters, nonCap);
                    }
                }
                else if (capList.Count > 0)
                {
                    var combined = new List<QuestEntry>(list);
                    combined.AddRange(bonus);
                    AddFieldGroupsSplitByWeapon(allGroups, region, spLabel, "★포획", monsters, combined);
                }
                else
                {
                    var combined = new List<QuestEntry>(list);
                    if (canStackRadiant)
                    {
                        combined.AddRange(regionRadiants);
                        foreach (var rq in regionRadiants) usedRadiantIds.Add(rq.Id);
                    }
                    combined.AddRange(bonus);
                    string lbl = canStackRadiant ? ("융광종(" + spLabel + ")") : spLabel;
                    string act = slayList.Count > 0 ? "★토벌" : "★사냥";
                    AddFieldGroupsSplitByWeapon(allGroups, region, lbl, act, canStackRadiant ? radiantMonsters : monsters, combined);
                }

                foreach (var bq in bonus) usedAnyFieldIds.Add(bq.Id);
            }

            // 3. Remaining Radiant (융광종) quests: pair with any-field species/weapon quests using Radiant-ONLY monster list!
            var availableAnyFieldForRadiant = anyFieldQuests.Where(q => !usedAnyFieldIds.Contains(q.Id)).GroupBy(q => q.Species ?? "").ToList();
            var radiantConsumedAnyFieldIds = new HashSet<string>();
            foreach (var kv in radiantByRegion)
            {
                string region = kv.Key;
                var rem = kv.Value.Where(q => !usedRadiantIds.Contains(q.Id)).ToList();
                if (rem.Count == 0) continue;

                if (availableAnyFieldForRadiant.Count > 0)
                {
                    foreach (var grp in availableAnyFieldForRadiant)
                    {
                        var combined = new List<QuestEntry>(rem);
                        combined.AddRange(grp.ToList());
                        string sp = grp.Key;
                        string lbl = string.IsNullOrEmpty(sp) ? "융광종" : ("융광종(" + sp + ")");
                        string radiantOnlyMonsters = GetSpeciesMonsters(sp, true);
                        AddFieldGroupsSplitByWeapon(allGroups, region, lbl, "★사냥", radiantOnlyMonsters, combined);
                        foreach (var aq in grp) radiantConsumedAnyFieldIds.Add(aq.Id);
                    }
                }
                else
                {
                    AddFieldGroupsSplitByWeapon(allGroups, region, "융광종", "★사냥", "", rem);
                }
            }
            foreach (var id in radiantConsumedAnyFieldIds) usedAnyFieldIds.Add(id);

            // 4. Leftover any-field quests
            var leftoverAnyField = anyFieldQuests.Where(q => !usedAnyFieldIds.Contains(q.Id)).ToList();
            if (leftoverAnyField.Count > 0)
            {
                var bySp = leftoverAnyField.GroupBy(q => q.Species ?? "");
                foreach (var grp in bySp)
                {
                    string sp = grp.Key;
                    var items = grp.ToList();
                    AddFieldGroupsSplitByWeapon(allGroups, "아무 필드", string.IsNullOrEmpty(sp) ? "자유" : sp, "★사냥", GetSpeciesMonsters(sp, false), items);
                }
            }

            // Sort strictly by highest simultaneous clear count descending (Nest does NOT force-override higher counts!)
            allGroups.Sort((a, b) =>
            {
                int cmp = b.Count.CompareTo(a.Count);
                if (cmp != 0) return cmp;
                if (a.IsNestGroup != b.IsNestGroup) return a.IsNestGroup ? -1 : 1;
                return string.Compare(a.RegionTitle, b.RegionTitle, StringComparison.Ordinal);
            });

            result.AddRange(allGroups);
            return result;
        }

        private static void AddFieldGroupsSplitByWeapon(List<QuestPlanGroup> target, string region, string spLabel, string actionBadge, string monsters, List<QuestEntry> combined)
        {
            var ranged = combined.Where(x => x.WeaponType == "원거리").ToList();
            var melee = combined.Where(x => x.WeaponType == "근거리").ToList();
            var neutral = combined.Where(x => string.IsNullOrEmpty(x.WeaponType)).ToList();

            // Never combine ranged (원거리) and melee (근거리) weapon quests into a single simultaneous card!
            if (ranged.Count > 0 && melee.Count > 0)
            {
                var rList = new List<QuestEntry>(neutral);
                rList.AddRange(ranged);
                target.Add(MakeFieldGroup(region, spLabel, actionBadge, monsters, rList));

                var mList = new List<QuestEntry>(neutral);
                mList.AddRange(melee);
                target.Add(MakeFieldGroup(region, spLabel, actionBadge, monsters, mList));
            }
            else
            {
                target.Add(MakeFieldGroup(region, spLabel, actionBadge, monsters, combined));
            }
        }

        private static QuestPlanGroup MakeFieldGroup(string region, string spLabel, string actionBadge, string monsters, List<QuestEntry> combined)
        {
            var weaponTips = combined.Where(x => !string.IsNullOrEmpty(x.WeaponType)).Select(x => x.WeaponType).Distinct().ToList();
            string wBadge = weaponTips.Count > 0 ? (string.Join("/", weaponTips.ToArray()) + " 무기") : "";
            string wTag = string.IsNullOrEmpty(wBadge) ? "" : (" · " + wBadge);

            return new QuestPlanGroup
            {
                Header = string.Format("[{0} - {1}]  {2}{3}  — {4}개", region, spLabel, actionBadge, wTag, combined.Count),
                RegionTitle = region,
                SpeciesTitle = spLabel,
                ActionBadge = actionBadge,
                WeaponBadge = wBadge,
                TargetMonsters = monsters,
                Tip = "",
                Quests = combined
            };
        }

        public static string GetTopRecommendation(List<QuestPlanGroup> plans, int totalCount)
        {
            if (totalCount == 0 || plans == null || plans.Count == 0)
                return "게임에서 퀘스트를 클릭한 뒤 [F8]을 눌러 수집하세요. (최대 10개)";

            var first = plans[0];
            if (first.Count >= 2)
                return string.Format("🔥 최고 효율 ({0}개 동시): [{1} - {2}] {3}{4}",
                    first.Count, first.RegionTitle, first.SpeciesTitle, first.ActionBadge,
                    string.IsNullOrEmpty(first.WeaponBadge) ? "" : (" · " + first.WeaponBadge));

            if (first.IsNestGroup)
                return string.Format("⚡ 겹치는 조합 없음 — [둥지] {0}개 먼저 처리 후 새 퀘스트 받기!", first.Count);

            return string.Format("💡 현재 겹치는 조합 없음 ({0}/10) — 빠른 퀘스트 1개 처리 후 새로 받아보세요.", totalCount);
        }

        private static string CleanLeadingBullet(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // Strip OCR bullet artifacts like "0 ", "◆ ", ":心", "• "
            return Regex.Replace(s.Trim(), @"^[\s:·•◆◇○●□■0oO心\-\|]+(?=[가-힣A-Za-z])", "").Trim();
        }

        private static string FindBestMatch(string text, string[] candidates, int maxDist)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string norm = text.Replace(" ", "");

            // 1. Exact substring match first
            for (int i = 0; i < candidates.Length; i++)
            {
                string cNorm = candidates[i].Replace(" ", "");
                if (norm.Contains(cNorm)) return candidates[i];
            }

            // 2. Sliding-window Levenshtein fuzzy match (catches 1-char OCR typos like 산란한군도 -> 찬란한군도)
            int bestDist = maxDist + 1;
            string best = "";
            for (int i = 0; i < candidates.Length; i++)
            {
                string cNorm = candidates[i].Replace(" ", "");
                int len = cNorm.Length;
                if (norm.Length < len) continue;
                for (int start = 0; start <= norm.Length - len; start++)
                {
                    int d = Levenshtein(norm.Substring(start, len), cNorm);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = candidates[i];
                    }
                }
            }
            return bestDist <= maxDist ? best : "";
        }

        private static string ReplaceFuzzySubstring(string text, string target, int maxDist)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(target)) return text;
            if (text.Contains(target)) return text;
            int len = target.Length;
            if (text.Length < len) return text;
            for (int start = 0; start <= text.Length - len; start++)
            {
                string sub = text.Substring(start, len);
                if (Levenshtein(sub, target) <= maxDist)
                    return text.Substring(0, start) + target + text.Substring(start + len);
            }
            return text;
        }

        public static bool IsLikelyDuplicate(QuestEntry a, QuestEntry b)
        {
            if (a == null || b == null) return false;
            if (a.RawFingerprint == b.RawFingerprint) return true;

            // Same parsed attributes (Region + Species + Action + Weapon) + similar title (<= 2 OCR char diffs)
            bool sameMeta = a.Region == b.Region && a.Species == b.Species && a.ActionType == b.ActionType && a.WeaponType == b.WeaponType;
            string ta = (a.Title ?? "").Replace(" ", "");
            string tb = (b.Title ?? "").Replace(" ", "");
            if (sameMeta && !string.IsNullOrEmpty(ta) && !string.IsNullOrEmpty(tb))
            {
                int maxDiff = Math.Max(2, Math.Min(ta.Length, tb.Length) / 4);
                if (Levenshtein(ta, tb) <= maxDiff)
                    return true;
            }

            // Or almost identical objective text (<= 2 char diffs)
            string oa = (a.ObjectiveText ?? "").Replace(" ", "");
            string ob = (b.ObjectiveText ?? "").Replace(" ", "");
            if (sameMeta && !string.IsNullOrEmpty(oa) && !string.IsNullOrEmpty(ob) && Levenshtein(oa, ob) <= 2)
                return true;

            return false;
        }

        public static void AppendToQuestLog(QuestEntry e)
        {
            if (e == null) return;
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quest_history.csv");
                bool writeHeader = !File.Exists(path);
                using (var sw = new StreamWriter(path, true, new System.Text.UTF8Encoding(true)))
                {
                    if (writeHeader)
                        sw.WriteLine("Timestamp,Title,Region,Species,Action,Weapon,Progress,Objective");
                    sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        CsvEsc(e.Title),
                        CsvEsc(e.Region),
                        CsvEsc(e.Species),
                        CsvEsc(e.ActionType),
                        CsvEsc(e.WeaponType),
                        CsvEsc(e.Progress),
                        CsvEsc(e.ObjectiveText)));
                }
            }
            catch { }
        }

        private static string CsvEsc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static int Levenshtein(string a, string b)
        {
            int n = a.Length, m = b.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= m; j++) dp[0, j] = j;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int del = dp[i - 1, j] + 1;
                    int ins = dp[i, j - 1] + 1;
                    int sub = dp[i - 1, j - 1] + cost;
                    dp[i, j] = Math.Min(Math.Min(del, ins), sub);
                }
            }
            return dp[n, m];
        }
    }
}
