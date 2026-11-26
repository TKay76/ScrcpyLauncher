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

        [System.Web.Script.Serialization.ScriptIgnore]
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
            "융광종 리오레이아 아종", "융광종 디아블로스 아종",
            "융광종 도스쟈그라스", "융광종 도스기르오스", "융광종 쿠루루야크", "융광종 쥬라토도스",
            "융광종 토비카가치", "융광종 파오우르무", "융광종 레이기에나", "융광종 리오레이아",
            "융광종 리오레우스", "융광종 디아블로스", "융광종 라도발킨", "융광종 푸케푸케",
            "융광종 볼보로스", "융광종 안쟈나프", "융광종 치치야크", "융광종 오도가론",
            "리오레이아 아종", "디아블로스 아종", "루가 에알다",
            "도스쟈그라스", "도스기르오스", "쿠루루야크", "쥬라토도스",
            "토비카가치", "파오우르무", "레이기에나", "리오레이아",
            "리오레우스", "디아블로스", "라도발킨", "크샬다오라", "네르기간테",
            "푸케푸케", "볼보로스", "안쟈나프", "치치야크", "오도가론"
        };

        public class MonsterDef
        {
            public string Name;
            public string Species;
            public bool IsRadiant;
            public string[] Regions;
        }

        // Cross-verified across Game8, Gamerch, GameWith, and MHOBuilds
        public static readonly MonsterDef[] MonsterCatalog = new[]
        {
            // 아룡종
            new MonsterDef { Name = "도스쟈그라스", Species = "아룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "찬란한 군도" } },
            new MonsterDef { Name = "도스기르오스", Species = "아룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "부패의 심연" } },
            new MonsterDef { Name = "토비카가치", Species = "아룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "찬란한 군도" } },
            new MonsterDef { Name = "오도가론", Species = "아룡종", IsRadiant = false, Regions = new[] { "부패의 심연", "찬란한 군도" } },
            new MonsterDef { Name = "융광종 도스쟈그라스", Species = "아룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "부패의 심연", "찬란한 군도" } },
            new MonsterDef { Name = "융광종 도스기르오스", Species = "아룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "부패의 심연" } },
            new MonsterDef { Name = "융광종 토비카가치", Species = "아룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "부패의 심연" } },
            new MonsterDef { Name = "융광종 오도가론", Species = "아룡종", IsRadiant = true, Regions = new[] { "부패의 심연", "찬란한 군도" } },

            // 조룡종
            new MonsterDef { Name = "쿠루루야크", Species = "조룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "열사 평원" } },
            new MonsterDef { Name = "푸케푸케", Species = "조룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "열사 평원", "찬란한 군도" } },
            new MonsterDef { Name = "치치야크", Species = "조룡종", IsRadiant = false, Regions = new[] { "찬란한 군도" } },
            new MonsterDef { Name = "융광종 쿠루루야크", Species = "조룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "열사 평원", "부패의 심연" } },
            new MonsterDef { Name = "융광종 푸케푸케", Species = "조룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "열사 평원", "부패의 심연", "찬란한 군도" } },
            new MonsterDef { Name = "융광종 치치야크", Species = "조룡종", IsRadiant = true, Regions = new[] { "찬란한 군도" } },

            // 어룡종
            new MonsterDef { Name = "쥬라토도스", Species = "어룡종", IsRadiant = false, Regions = new[] { "열사 평원" } },
            new MonsterDef { Name = "융광종 쥬라토도스", Species = "어룡종", IsRadiant = true, Regions = new[] { "열사 평원" } },

            // 수룡종
            new MonsterDef { Name = "볼보로스", Species = "수룡종", IsRadiant = false, Regions = new[] { "열사 평원" } },
            new MonsterDef { Name = "안쟈나프", Species = "수룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "열사 평원", "찬란한 군도" } },
            new MonsterDef { Name = "라도발킨", Species = "수룡종", IsRadiant = false, Regions = new[] { "부패의 심연" } },
            new MonsterDef { Name = "융광종 볼보로스", Species = "수룡종", IsRadiant = true, Regions = new[] { "열사 평원" } },
            new MonsterDef { Name = "융광종 안쟈나프", Species = "수룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "열사 평원", "부패의 심연", "찬란한 군도" } },
            new MonsterDef { Name = "융광종 라도발킨", Species = "수룡종", IsRadiant = true, Regions = new[] { "부패의 심연" } },

            // 비룡종
            new MonsterDef { Name = "리오레이아", Species = "비룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "열사 평원" } },
            new MonsterDef { Name = "리오레이아 아종", Species = "비룡종", IsRadiant = false, Regions = new[] { "열사 평원", "부패의 심연", "찬란한 군도" } },
            new MonsterDef { Name = "리오레우스", Species = "비룡종", IsRadiant = false, Regions = new[] { "거목의 벌판", "열사 평원" } },
            new MonsterDef { Name = "디아블로스", Species = "비룡종", IsRadiant = false, Regions = new[] { "열사 평원", "부패의 심연" } },
            new MonsterDef { Name = "디아블로스 아종", Species = "비룡종", IsRadiant = false, Regions = new[] { "열사 평원" } },
            new MonsterDef { Name = "파오우르무", Species = "비룡종", IsRadiant = false, Regions = new[] { "찬란한 군도" } },
            new MonsterDef { Name = "레이기에나", Species = "비룡종", IsRadiant = false, Regions = new[] { "찬란한 군도" } },
            new MonsterDef { Name = "융광종 리오레이아", Species = "비룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "열사 평원", "부패의 심연" } },
            new MonsterDef { Name = "융광종 리오레이아 아종", Species = "비룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "열사 평원" } },
            new MonsterDef { Name = "융광종 리오레우스", Species = "비룡종", IsRadiant = true, Regions = new[] { "거목의 벌판", "열사 평원" } },
            new MonsterDef { Name = "융광종 디아블로스", Species = "비룡종", IsRadiant = true, Regions = new[] { "열사 평원" } },
            new MonsterDef { Name = "융광종 디아블로스 아종", Species = "비룡종", IsRadiant = true, Regions = new[] { "열사 평원" } },
            new MonsterDef { Name = "융광종 파오우르무", Species = "비룡종", IsRadiant = true, Regions = new[] { "찬란한 군도" } },
            new MonsterDef { Name = "융광종 레이기에나", Species = "비룡종", IsRadiant = true, Regions = new[] { "찬란한 군도" } },

            // 고룡종
            new MonsterDef { Name = "네르기간테", Species = "고룡종", IsRadiant = false, Regions = new[] { "거목의 벌판" } },
            new MonsterDef { Name = "루가 에알다", Species = "고룡종", IsRadiant = false, Regions = new string[0] },
            new MonsterDef { Name = "크샬다오라", Species = "고룡종", IsRadiant = false, Regions = new string[0] }
        };

        public static string GetMonstersForRegionAndSpecies(string region, string species, bool radiantOnly, bool includeRadiant)
        {
            if (species == "융광종")
            {
                radiantOnly = true;
                species = "";
            }
            if (string.IsNullOrEmpty(species) && !radiantOnly) return "";
            bool filterRegion = !string.IsNullOrEmpty(region) && FieldRegions.Contains(region);

            var names = new List<string>();
            for (int i = 0; i < MonsterCatalog.Length; i++)
            {
                var m = MonsterCatalog[i];
                if (!string.IsNullOrEmpty(species) && m.Species != species) continue;
                if (radiantOnly && !m.IsRadiant) continue;
                if (!radiantOnly && !includeRadiant && m.IsRadiant) continue;
                if (filterRegion && !m.Regions.Contains(region)) continue;
                names.Add(m.Name);
            }
            return string.Join(", ", names.ToArray());
        }

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

        public static string GetSpeciesMonsters(string species, bool radiantOnly = false, bool includeRadiant = false)
        {
            if (string.IsNullOrEmpty(species)) return "";
            string val;
            if (radiantOnly)
                return RadiantSpeciesMonstersMap.TryGetValue(species, out val) ? val : "";

            string baseList = SpeciesMonstersMap.TryGetValue(species, out val) ? val : "";
            if (includeRadiant)
            {
                string radList;
                if (RadiantSpeciesMonstersMap.TryGetValue(species, out radList) && !string.IsNullOrEmpty(radList))
                    return string.IsNullOrEmpty(baseList) ? radList : (baseList + ", " + radList);
            }
            return baseList;
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

            Bitmap bodyBmp = null;
            Bitmap titleBmp = null;
            try
            {
                var crops = CaptureQuestDetailCrops(targetHwnd, videoW, videoH);
                if (crops == null || crops.Item1 == null)
                    return Tuple.Create<QuestEntry, string>(null, "화면 캡처에 실패했습니다.");

                bodyBmp = crops.Item1;
                titleBmp = crops.Item2;

                string[] bodyLines = await RunWinRtOcrAsync(bodyBmp);
                if (bodyLines == null || bodyLines.Length == 0)
                    return Tuple.Create<QuestEntry, string>(null, "퀘스트 텍스트를 인식하지 못했습니다.");

                string extractedTitle = "";
                if (titleBmp != null)
                {
                    try
                    {
                        string[] titleLines = await RunWinRtOcrAsync(titleBmp);
                        if (titleLines != null && titleLines.Length > 0)
                            extractedTitle = CleanLeadingBullet(string.Join(" ", titleLines)).Trim();
                    }
                    catch { }
                }

                QuestEntry entry = ParseQuestLines(bodyLines, extractedTitle);
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
                if (bodyBmp != null) bodyBmp.Dispose();
                if (titleBmp != null) titleBmp.Dispose();
            }
        }

        private static Tuple<Bitmap, Bitmap> CaptureQuestDetailCrops(IntPtr hwnd, int videoW, int videoH)
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
                return CropQuestRegionsFromBitmap(full, videoW, videoH);
            }
        }

        public static Tuple<Bitmap, Bitmap> CropQuestRegionsFromBitmap(Bitmap full, int videoW, int videoH)
        {
            if (full == null) return null;
            int cw = full.Width;
            int ch = full.Height;
            if (cw < 200 || ch < 120) return null;

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

            // 1. Body-only crop (Region + Objective + Progress ONLY: x: 48%..97%, y: 22.0%..33.5%)
            // Completely excludes the brown title ribbon above and flavor description below!
            int rx = Math.Max(0, (int)Math.Round(vx + vw * 0.48));
            int ry = Math.Max(0, (int)Math.Round(vy + vh * 0.220));
            int rw = Math.Min(cw - rx, (int)Math.Round(vw * 0.49));
            int rh = Math.Min(ch - ry, (int)Math.Round(vh * 0.115));
            if (rw < 80 || rh < 24) return null;

            var bodyScaled = new Bitmap(rw * 2, rh * 2, PixelFormat.Format32bppArgb);
            using (var sg = Graphics.FromImage(bodyScaled))
            {
                sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                sg.DrawImage(full, new Rectangle(0, 0, bodyScaled.Width, bodyScaled.Height), new Rectangle(rx, ry, rw, rh), GraphicsUnit.Pixel);
            }
            EnhanceParchmentContrast(bodyScaled);

            // 2. Title-only crop (for quest_history.csv logging ONLY: x: 48%..97%, y: 15.5%..22.0%)
            Bitmap titleScaled = null;
            int ty = Math.Max(0, (int)Math.Round(vy + vh * 0.155));
            int th = Math.Min(ch - ty, (int)Math.Round(vh * 0.065));
            if (rw >= 80 && th >= 14)
            {
                titleScaled = new Bitmap(rw * 2, th * 2, PixelFormat.Format32bppArgb);
                using (var tg = Graphics.FromImage(titleScaled))
                {
                    tg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    tg.DrawImage(full, new Rectangle(0, 0, titleScaled.Width, titleScaled.Height), new Rectangle(rx, ty, rw, th), GraphicsUnit.Pixel);
                }
            }

            return Tuple.Create(bodyScaled, titleScaled);
        }

        private static void EnhanceParchmentContrast(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * h;
                byte[] buf = new byte[bytes];
                Marshal.Copy(data.Scan0, buf, 0, bytes);

                for (int y = 0; y < h; y++)
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

        public static QuestEntry ParseQuestLines(string[] lines, string explicitTitle = null)
        {
            if (lines == null || lines.Length < 1) return null;

            string allText = string.Join(" ", lines);

            // Extract progress like 0/1, 1/3, 0/5
            string progress = "";
            var pm = Regex.Match(allText, @"\b(\d+\s*/\s*\d+)\b");
            if (pm.Success) progress = pm.Groups[1].Value.Replace(" ", "");

            // Detect objective line by keyword priority score (prevents long titles like "사막 비룡의 이동 경로 추적" from overriding real objectives!)
            string objective = "";
            int bestScore = 0;
            int objIdx = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = CleanLeadingBullet(lines[i]);
                if (string.IsNullOrEmpty(l) || Regex.IsMatch(l, @"^\d+\s*/\s*\d+$")) continue;

                int score = 0;
                if (l.Contains("포획") || l.Contains("토벌") || l.Contains("사냥") || l.Contains("수렵") || l.Contains("파괴")) score += 12;
                if (l.Contains("에서") || l.Contains("원거리") || l.Contains("근거리") || l.Contains("근접") || l.Contains("난이도")) score += 8;
                if (Regex.IsMatch(l, @"\d+\s*/\s*\d+")) score += 6;
                if (l.Contains("둥지") || l.Contains("등지") || l.Contains("동지")) score += 4;

                bool exactSpecies = false;
                for (int s = 0; s < AllSpecies.Length; s++)
                {
                    if (l.Contains(AllSpecies[s])) { exactSpecies = true; break; }
                }
                if (exactSpecies) score += 5;
                else if (FindBestMatch(l, AllSpecies, 1) != "") score += 1;

                if (FindBestMatch(l, AllMonsterNames, 1) != "") score += 4;
                if (score > 0 && lines.Length >= 2 && i > 0) score += 3;

                if (score > bestScore || (score > 0 && score == bestScore && l.Length > objective.Length))
                {
                    bestScore = score;
                    objective = l;
                    objIdx = i;
                }
            }

            if (string.IsNullOrEmpty(objective))
                return null;

            // Remove trailing progress from objective
            objective = Regex.Replace(objective, @"\s*\d+\s*/\s*\d+\s*$", "").Trim();

            // Detect quest title: prefer explicitTitle from dedicated title-ribbon crop (for quest_history.csv logging ONLY)
            string title = (explicitTitle ?? "").Trim();
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

            // Detect Monster Species & specific Monster Name FIRST (helps catch Nest quests even if "둥지" has an OCR typo!)
            string species = FindBestMatch(allText, AllSpecies, 1);
            string targetMonster = FindBestMatch(objective, AllMonsterNames, 1);
            if (!string.IsNullOrEmpty(targetMonster))
                objective = ReplaceFuzzySubstring(objective, targetMonster, 1);

            // Detect Weapon condition (원거리 / 근거리 · 근접)
            string weapon = "";
            if (allText.Contains("원거리")) weapon = "원거리";
            else if (allText.Contains("근거리") || allText.Contains("근접")) weapon = "근거리";

            // Detect Region (Field 4 + 둥지 + 필드 무관)
            string region = "";
            bool isNest = false;
            bool isAnyField = false;

            if (allText.Contains("둥지") || allText.Contains("등지") || allText.Contains("동지") || allText.Contains("난이도"))
            {
                region = "둥지";
                isNest = true;
            }
            else
            {
                region = FindBestMatch(allText, FieldRegions, 1);
            }

            if (string.IsNullOrEmpty(region))
            {
                if (allText.Contains("필드") || !string.IsNullOrEmpty(weapon))
                {
                    region = "필드 (지역 무관)";
                    isAnyField = true;
                }
                else if (!string.IsNullOrEmpty(targetMonster))
                {
                    // Specific monster name with no field region (e.g. "푸케푸케 포획") is a Nest quest!
                    region = "둥지";
                    isNest = true;
                }
                else
                {
                    // Never treat unknown/unclassified quests as IsAnyField so they never pollute field cards!
                    region = "기타/미분류";
                    isAnyField = false;
                }
            }

            // Detect Action Type (포획 / 토벌 / 사냥 / 기타)
            string action = "기타";
            if (objective.Contains("포획")) action = "포획";
            else if (objective.Contains("토벌")) action = "토벌";
            else if (objective.Contains("사냥") || objective.Contains("수렵")) action = "사냥";
            else if (allText.Contains("포획")) action = "포획";
            else if (allText.Contains("토벌")) action = "토벌";
            else if (allText.Contains("사냥") || allText.Contains("수렵")) action = "사냥";
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

        public static List<QuestPlanGroup> BuildSimultaneousPlans(List<QuestEntry> quests, bool nestFirst = false)
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
                string monstersCap = GetMonstersForRegionAndSpecies(region, rawSp, false, false);      // 포획: 해당 지역 일반/아종만
                string monstersAll = GetMonstersForRegionAndSpecies(region, rawSp, false, true);       // 사냥/토벌: 해당 지역 일반/아종 + 융광종
                string radiantMonsters = GetMonstersForRegionAndSpecies(region, rawSp, true, false);   // 융광종 조합: 해당 지역 융광종만

                var capList = list.Where(x => x.ActionType == "포획").ToList();
                var slayList = list.Where(x => x.ActionType == "토벌").ToList();
                var huntList = list.Where(x => x.ActionType != "포획" && x.ActionType != "토벌").ToList();

                var bonusNormal = new List<QuestEntry>();
                var bonusRadiant = new List<QuestEntry>();
                foreach (var af in anyFieldQuests)
                {
                    if (af.Species == "융광종")
                    {
                        bonusRadiant.Add(af);
                    }
                    else if (string.IsNullOrEmpty(af.Species) || string.IsNullOrEmpty(rawSp) || af.Species == rawSp)
                    {
                        bonusNormal.Add(af);
                    }
                }

                List<QuestEntry> regionRadiants;
                radiantByRegion.TryGetValue(region, out regionRadiants);
                bool hasNonCapture = (slayList.Count > 0 || huntList.Count > 0);
                // Only stack Radiant if this species actually spawns as Radiant in THIS region!
                bool hasAnyRadiantQuest = ((regionRadiants != null && regionRadiants.Count > 0) || bonusRadiant.Count > 0);
                bool canStackRadiant = (hasNonCapture && hasAnyRadiantQuest && !string.IsNullOrEmpty(radiantMonsters));

                if (capList.Count > 0 && (slayList.Count > 0 || canStackRadiant))
                {
                    var capCombined = new List<QuestEntry>();
                    capCombined.AddRange(capList);
                    capCombined.AddRange(huntList);
                    capCombined.AddRange(bonusNormal);
                    AddFieldGroupsSplitByWeapon(allGroups, region, spLabel, "★포획", monstersCap, capCombined);

                    var nonCap = new List<QuestEntry>();
                    nonCap.AddRange(slayList);
                    nonCap.AddRange(huntList);
                    if (canStackRadiant)
                    {
                        if (regionRadiants != null && regionRadiants.Count > 0)
                        {
                            nonCap.AddRange(regionRadiants);
                            foreach (var rq in regionRadiants) usedRadiantIds.Add(rq.Id);
                        }
                        if (bonusRadiant.Count > 0)
                        {
                            nonCap.AddRange(bonusRadiant);
                            foreach (var brq in bonusRadiant) usedAnyFieldIds.Add(brq.Id);
                        }
                    }
                    nonCap.AddRange(bonusNormal);
                    if (nonCap.Count > bonusNormal.Count)
                    {
                        string lbl = canStackRadiant ? ("융광종(" + spLabel + ")") : spLabel;
                        string act = slayList.Count > 0 ? "★토벌" : "★사냥";
                        AddFieldGroupsSplitByWeapon(allGroups, region, lbl, act, canStackRadiant ? radiantMonsters : monstersAll, nonCap);
                    }
                }
                else if (capList.Count > 0)
                {
                    var combined = new List<QuestEntry>(list);
                    combined.AddRange(bonusNormal);
                    AddFieldGroupsSplitByWeapon(allGroups, region, spLabel, "★포획", monstersCap, combined);
                }
                else
                {
                    var combined = new List<QuestEntry>(list);
                    if (canStackRadiant)
                    {
                        if (regionRadiants != null && regionRadiants.Count > 0)
                        {
                            combined.AddRange(regionRadiants);
                            foreach (var rq in regionRadiants) usedRadiantIds.Add(rq.Id);
                        }
                        if (bonusRadiant.Count > 0)
                        {
                            combined.AddRange(bonusRadiant);
                            foreach (var brq in bonusRadiant) usedAnyFieldIds.Add(brq.Id);
                        }
                    }
                    combined.AddRange(bonusNormal);
                    string lbl = canStackRadiant ? ("융광종(" + spLabel + ")") : spLabel;
                    string act = slayList.Count > 0 ? "★토벌" : "★사냥";
                    AddFieldGroupsSplitByWeapon(allGroups, region, lbl, act, canStackRadiant ? radiantMonsters : monstersAll, combined);
                }

                foreach (var bq in bonusNormal) usedAnyFieldIds.Add(bq.Id);
            }

            // 3. Remaining Radiant (융광종) quests: pair with any-field Radiant quests AND any-field species that ACTUALLY spawn as Radiant in THIS region!
            var availableAnyFieldForRadiant = anyFieldQuests.Where(q => !usedAnyFieldIds.Contains(q.Id)).GroupBy(q => q.Species ?? "").ToList();
            var radiantConsumedAnyFieldIds = new HashSet<string>();
            foreach (var kv in radiantByRegion)
            {
                string region = kv.Key;
                var rem = kv.Value.Where(q => !usedRadiantIds.Contains(q.Id)).ToList();
                if (rem.Count == 0) continue;

                // Any-field "융광종" quests (e.g. 필드에서 원거리/근접 무기로 융광종 사냥) ALWAYS match every regional Radiant quest!
                var afRadiantAll = anyFieldQuests.Where(q => !usedAnyFieldIds.Contains(q.Id) && q.Species == "융광종").ToList();
                var remWithAfRad = new List<QuestEntry>(rem);
                remWithAfRad.AddRange(afRadiantAll);
                foreach (var aq in afRadiantAll) radiantConsumedAnyFieldIds.Add(aq.Id);

                bool pairedSpecies = false;
                if (availableAnyFieldForRadiant.Count > 0)
                {
                    foreach (var grp in availableAnyFieldForRadiant)
                    {
                        string sp = grp.Key;
                        if (sp == "융광종") continue; // Already combined into remWithAfRad above
                        string radiantOnlyMonsters = GetMonstersForRegionAndSpecies(region, sp, true, false);
                        // Skip impossible combinations (e.g. 어룡종 does NOT spawn in 부패의 심연!)
                        if (!string.IsNullOrEmpty(sp) && string.IsNullOrEmpty(radiantOnlyMonsters))
                            continue;

                        var combined = new List<QuestEntry>(remWithAfRad);
                        combined.AddRange(grp.ToList());
                        string lbl = string.IsNullOrEmpty(sp) ? "융광종" : ("융광종(" + sp + ")");
                        AddFieldGroupsSplitByWeapon(allGroups, region, lbl, "★사냥", radiantOnlyMonsters, combined);
                        foreach (var aq in grp) radiantConsumedAnyFieldIds.Add(aq.Id);
                        pairedSpecies = true;
                    }
                }

                if (!pairedSpecies)
                {
                    string allRegRadiants = GetMonstersForRegionAndSpecies(region, "", true, false);
                    AddFieldGroupsSplitByWeapon(allGroups, region, "융광종", "★사냥", allRegRadiants, remWithAfRad);
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
                    AddFieldGroupsSplitByWeapon(allGroups, "아무 필드", string.IsNullOrEmpty(sp) ? "자유" : sp, "★사냥", GetMonstersForRegionAndSpecies("", sp, false, true), items);
                }
            }

            // Sort by Nest-first if enabled, otherwise strictly by highest simultaneous clear count descending
            allGroups.Sort((a, b) =>
            {
                if (nestFirst && a.IsNestGroup != b.IsNestGroup)
                    return a.IsNestGroup ? -1 : 1;
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

        public class QuestTitleTemplate
        {
            public string Title { get; set; }
            public string Region { get; set; }
            public bool IsNest { get; set; }
            public bool IsAnyField { get; set; }
            public string Species { get; set; }
            public string TargetMonster { get; set; }
            public string ActionType { get; set; }
            public string WeaponType { get; set; }
            public string ObjectiveText { get; set; }
        }

        public class BatchScanResult
        {
            public List<QuestEntry> Matched { get; set; }
            public List<string> UnknownTitles { get; set; }
            public string ErrorMessage { get; set; }
        }

        private static readonly QuestTitleTemplate[] BuiltInTitleTemplates = new[]
        {
            // Field — Capture
            new QuestTitleTemplate { Title = "아마추어 조사대의 한계", Region = "찬란한 군도", Species = "아룡종", ActionType = "포획", ObjectiveText = "찬란한 군도에서 아룡종 포획" },
            new QuestTitleTemplate { Title = "신비의 묘약", Region = "열사 평원", Species = "수룡종", ActionType = "포획", ObjectiveText = "열사 평원에서 수룡종 포획" },
            new QuestTitleTemplate { Title = "수수께끼의 식재료", Region = "거목의 벌판", Species = "수룡종", ActionType = "포획", ObjectiveText = "거목의 벌판에서 수룡종 포획" },
            new QuestTitleTemplate { Title = "혼신의 한 권", Region = "열사 평원", Species = "조룡종", ActionType = "포획", ObjectiveText = "열사 평원에서 조룡종 포획" },
            new QuestTitleTemplate { Title = "하늘을 나는 새와 숨겨진 보물", Region = "거목의 벌판", Species = "조룡종", ActionType = "포획", ObjectiveText = "거목의 벌판에서 조룡종 포획" },
            new QuestTitleTemplate { Title = "군도 맹수 연구", Region = "찬란한 군도", Species = "수룡종", ActionType = "포획", ObjectiveText = "찬란한 군도에서 수룡종 포획" },
            new QuestTitleTemplate { Title = "검술의 극치를 찾아서", Region = "부패의 심연", Species = "아룡종", ActionType = "포획", ObjectiveText = "부패의 심연에서 아룡종 포획" },
            new QuestTitleTemplate { Title = "몬스터 이사 대작전", Region = "거목의 벌판", Species = "비룡종", ActionType = "포획", ObjectiveText = "거목의 벌판에서 비룡종 포획" },
            new QuestTitleTemplate { Title = "사막 비룡의 이동 경로 추적", Region = "열사 평원", Species = "비룡종", ActionType = "포획", ObjectiveText = "열사 평원에서 비룡종 포획" },
            new QuestTitleTemplate { Title = "신비로운 부패의 심연", Region = "부패의 심연", Species = "수룡종", ActionType = "포획", ObjectiveText = "부패의 심연에서 수룡종 포획" },

            // Field — Hunt / Radiant
            new QuestTitleTemplate { Title = "심연 밑바닥의 강렬한 빛", Region = "부패의 심연", Species = "융광종", ActionType = "사냥", ObjectiveText = "부패의 심연에서 융광종 사냥" },
            new QuestTitleTemplate { Title = "좀 더 가까운 곳에서", Region = "열사 평원", Species = "융광종", ActionType = "사냥", ObjectiveText = "열사 평원에서 융광종 사냥" },
            new QuestTitleTemplate { Title = "요리사의 분노", Region = "찬란한 군도", Species = "융광종", ActionType = "사냥", ObjectiveText = "찬란한 군도에서 융광종 사냥" },
            new QuestTitleTemplate { Title = "새 대량 발생", Region = "거목의 벌판", Species = "조룡종", ActionType = "사냥", ObjectiveText = "거목의 벌판에서 조룡종 사냥" },
            new QuestTitleTemplate { Title = "골짜기 바닥의 진동", Region = "부패의 심연", Species = "수룡종", ActionType = "사냥", ObjectiveText = "부패의 심연에서 수룡종 사냥" },
            new QuestTitleTemplate { Title = "또다시 빈손으로 귀환", Region = "열사 평원", Species = "어룡종", ActionType = "사냥", ObjectiveText = "열사 평원에서 어룡종 사냥" },
            new QuestTitleTemplate { Title = "무모한 사막의 거수", Region = "열사 평원", Species = "수룡종", ActionType = "사냥", ObjectiveText = "열사 평원에서 수룡종 사냥" },
            new QuestTitleTemplate { Title = "비룡의 날치기 배송", Region = "열사 평원", Species = "비룡종", ActionType = "사냥", ObjectiveText = "열사 평원에서 비룡종 사냥" },

            // Any-Field — Weapon Condition
            new QuestTitleTemplate { Title = "일정 대지연", Region = "필드 (지역 무관)", IsAnyField = true, Species = "비룡종", ActionType = "사냥", WeaponType = "원거리", ObjectiveText = "필드에서 원거리 무기를 사용해 비룡종 사냥" },
            new QuestTitleTemplate { Title = "낚시 성과 위장 대작전", Region = "필드 (지역 무관)", IsAnyField = true, Species = "어룡종", ActionType = "사냥", WeaponType = "원거리", ObjectiveText = "필드에서 원거리 무기를 사용해 어룡종 사냥" },
            new QuestTitleTemplate { Title = "칼날에 의한 징벌", Region = "필드 (지역 무관)", IsAnyField = true, Species = "조룡종", ActionType = "사냥", WeaponType = "근거리", ObjectiveText = "필드에서 근접 무기를 사용해 조룡종 사냥" },
            new QuestTitleTemplate { Title = "신선도를 유지하기 위한 시도", Region = "필드 (지역 무관)", IsAnyField = true, Species = "아룡종", ActionType = "사냥", WeaponType = "원거리", ObjectiveText = "필드에서 원거리 무기를 사용해 아룡종 사냥" },
            new QuestTitleTemplate { Title = "궁극의 맛을 찾아서", Region = "필드 (지역 무관)", IsAnyField = true, Species = "어룡종", ActionType = "사냥", WeaponType = "근거리", ObjectiveText = "필드에서 근접 무기를 사용해 어룡종 사냥" },
            new QuestTitleTemplate { Title = "외출 시 호신술", Region = "필드 (지역 무관)", IsAnyField = true, Species = "융광종", ActionType = "사냥", WeaponType = "원거리", ObjectiveText = "필드에서 원거리 무기를 사용해 융광종 사냥" },

            // Nest (Instance)
            new QuestTitleTemplate { Title = "태양보다 눈부신 녀석", Region = "둥지", IsNest = true, TargetMonster = "치치야크", ActionType = "사냥", ObjectiveText = "둥지에서 치치야크 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "여왕은 오직 하나", Region = "둥지", IsNest = true, TargetMonster = "리오레이아", ActionType = "사냥", ObjectiveText = "둥지에서 리오레이아 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "전율의 스파이스", Region = "둥지", IsNest = true, TargetMonster = "도스기르오스", ActionType = "사냥", ObjectiveText = "둥지에서 도스기르오스 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "글라이딩의 신비", Region = "둥지", IsNest = true, TargetMonster = "토비카가치", ActionType = "포획", ObjectiveText = "둥지에서 토비카가치 포획(난이도V)" },
            new QuestTitleTemplate { Title = "비밀 의뢰", Region = "둥지", IsNest = true, TargetMonster = "푸케푸케", ActionType = "포획", ObjectiveText = "둥지에서 푸케푸케 포획(난이도V)" },
            new QuestTitleTemplate { Title = "좋아하는 데 이유는 없다", Region = "둥지", IsNest = true, Species = "융광종", TargetMonster = "융광종 치치야크", ActionType = "사냥", ObjectiveText = "둥지에서 융광종 치치야크 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "쿨한 결단", Region = "둥지", IsNest = true, TargetMonster = "디아블로스", ActionType = "사냥", ObjectiveText = "둥지에서 디아블로스 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "분노의 심판", Region = "둥지", IsNest = true, TargetMonster = "안쟈나프", ActionType = "사냥", ObjectiveText = "둥지에서 안쟈나프 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "늦잠을 방해하는 자", Region = "둥지", IsNest = true, TargetMonster = "볼보로스", ActionType = "사냥", ObjectiveText = "둥지에서 볼보로스 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "꽃놀이하기 딱 좋은 날", Region = "둥지", IsNest = true, TargetMonster = "리오레이아 아종", ActionType = "포획", ObjectiveText = "둥지에서 리오레이아 아종 포획(난이도V)" },
            new QuestTitleTemplate { Title = "진흙 냄새가 바로 소울!", Region = "둥지", IsNest = true, TargetMonster = "쥬라토도스", ActionType = "사냥", ObjectiveText = "둥지에서 쥬라토도스 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "뜻밖의 만남", Region = "둥지", IsNest = true, Species = "융광종", TargetMonster = "융광종 오도가론", ActionType = "사냥", ObjectiveText = "둥지에서 융광종 오도가론 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "골짜기 밑에 잠들라", Region = "둥지", IsNest = true, TargetMonster = "라도발킨", ActionType = "포획", ObjectiveText = "둥지에서 라도발킨 포획(난이도V)" },
            new QuestTitleTemplate { Title = "'전승'의 첫 실전", Region = "둥지", IsNest = true, TargetMonster = "안쟈나프", ActionType = "포획", ObjectiveText = "둥지에서 안쟈나프 포획(난이도V)" },
            new QuestTitleTemplate { Title = "안전한 글라이딩", Region = "둥지", IsNest = true, TargetMonster = "리오레우스", ActionType = "사냥", ObjectiveText = "둥지에서 리오레우스 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "가출한 아내의 행방", Region = "둥지", IsNest = true, Species = "융광종", TargetMonster = "융광종 리오레이아", ActionType = "사냥", ObjectiveText = "둥지에서 융광종 리오레이아 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "최고의 별미는 꼬리에 있다", Region = "둥지", IsNest = true, Species = "융광종", TargetMonster = "융광종 볼보로스", ActionType = "사냥", ObjectiveText = "둥지에서 융광종 볼보로스 사냥(난이도V)" },
            new QuestTitleTemplate { Title = "설욕의 낚시", Region = "둥지", IsNest = true, TargetMonster = "쥬라토도스", ActionType = "포획", ObjectiveText = "둥지에서 쥬라토도스 포획(난이도V)" }
        };

        private static List<QuestTitleTemplate> _learnedTitleCache = null;

        public static string GetTitleDbPath()
        {
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(dir, "quest_title_db.json");
        }

        private static List<QuestTitleTemplate> GetAllTitleTemplates()
        {
            if (_learnedTitleCache == null)
            {
                _learnedTitleCache = new List<QuestTitleTemplate>();
                try
                {
                    string path = GetTitleDbPath();
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                        var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                        var loaded = ser.Deserialize<List<QuestTitleTemplate>>(json);
                        if (loaded != null) _learnedTitleCache = loaded;
                    }
                }
                catch { }
            }
            var all = new List<QuestTitleTemplate>(BuiltInTitleTemplates);
            all.AddRange(_learnedTitleCache);
            return all;
        }

        private static string NormalizeTitleKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s, @"[\s\p{P}\p{S}]", "").Trim();
        }

        public static void LearnQuestTitle(QuestEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Title) || string.IsNullOrWhiteSpace(entry.ObjectiveText))
                return;
            string norm = NormalizeTitleKey(entry.Title);
            if (norm.Length < 3 || norm == NormalizeTitleKey(entry.ObjectiveText))
                return;
            if (entry.Region == "기타/미분류")
                return;

            var all = GetAllTitleTemplates();
            for (int i = 0; i < all.Count; i++)
            {
                if (NormalizeTitleKey(all[i].Title) == norm)
                    return; // Already known
            }

            var tpl = new QuestTitleTemplate
            {
                Title = entry.Title.Trim(),
                Region = entry.Region ?? "",
                IsNest = entry.IsNest,
                IsAnyField = entry.IsAnyField,
                Species = entry.Species ?? "",
                TargetMonster = entry.TargetMonster ?? "",
                ActionType = entry.ActionType ?? "기타",
                WeaponType = entry.WeaponType ?? "",
                ObjectiveText = entry.ObjectiveText ?? ""
            };
            _learnedTitleCache.Add(tpl);
            try
            {
                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                File.WriteAllText(GetTitleDbPath(), ser.Serialize(_learnedTitleCache), System.Text.Encoding.UTF8);
            }
            catch { }
        }

        public static QuestTitleTemplate MatchTitleTemplate(string ocrLine)
        {
            string norm = NormalizeTitleKey(ocrLine);
            if (norm.Length < 3) return null;

            var all = GetAllTitleTemplates();
            QuestTitleTemplate best = null;
            int bestDist = int.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                string target = NormalizeTitleKey(all[i].Title);
                if (string.IsNullOrEmpty(target)) continue;
                if (norm == target) return all[i];

                int maxAllowed = target.Length >= 8 ? 2 : (target.Length >= 5 ? 1 : 0);
                if (Math.Abs(norm.Length - target.Length) > maxAllowed) continue;

                int d = Levenshtein(norm, target);
                if (d <= maxAllowed && d < bestDist)
                {
                    bestDist = d;
                    best = all[i];
                }
            }
            return best;
        }

        public static Bitmap CropQuestListFromBitmap(Bitmap full, int videoW, int videoH)
        {
            if (full == null) return null;
            int cw = full.Width;
            int ch = full.Height;
            if (cw < 200 || ch < 120) return null;

            double vx = 0, vy = 0, vw = cw, vh = ch;
            if (videoW > 0 && videoH > 0)
            {
                double winAspect = (double)cw / ch;
                double vidAspect = (double)videoW / videoH;
                if (winAspect > vidAspect + 0.002) { vh = ch; vw = ch * vidAspect; vx = (cw - vw) / 2.0; }
                else if (winAspect < vidAspect - 0.002) { vw = cw; vh = cw / vidAspect; vy = (ch - vh) / 2.0; }
            }

            // Left quest list column (titles only: x: 24.0%..42.5%, y: 8.5%..96.0%)
            // Starts at y=8.5% so scrolled-down lists do not clip the topmost quest!
            int rx = Math.Max(0, (int)Math.Round(vx + vw * 0.240));
            int ry = Math.Max(0, (int)Math.Round(vy + vh * 0.085));
            int rw = Math.Min(cw - rx, (int)Math.Round(vw * 0.185));
            int rh = Math.Min(ch - ry, (int)Math.Round(vh * 0.875));
            if (rw < 60 || rh < 80) return null;

            var scaled = new Bitmap(rw * 2, rh * 2, PixelFormat.Format32bppArgb);
            using (var sg = Graphics.FromImage(scaled))
            {
                sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                sg.DrawImage(full, new Rectangle(0, 0, scaled.Width, scaled.Height), new Rectangle(rx, ry, rw, rh), GraphicsUnit.Pixel);
            }
            EnhanceParchmentContrast(scaled);
            return scaled;
        }

        public static async Task<BatchScanResult> ScanQuestListFromBitmapAsync(Bitmap full, int videoW, int videoH)
        {
            var result = new BatchScanResult { Matched = new List<QuestEntry>(), UnknownTitles = new List<string>(), ErrorMessage = "" };
            Bitmap listBmp = null;
            try
            {
                listBmp = CropQuestListFromBitmap(full, videoW, videoH);
                if (listBmp == null)
                {
                    result.ErrorMessage = "왼쪽 퀘스트 목록 영역 캡처에 실패했습니다.";
                    return result;
                }

                string[] lines = await RunWinRtOcrAsync(listBmp);
                if (lines == null || lines.Length == 0)
                {
                    result.ErrorMessage = "왼쪽 퀘스트 목록 텍스트를 인식하지 못했습니다.";
                    return result;
                }

                var candidateLines = new List<string>();
                foreach (var raw in lines)
                {
                    string clean = CleanLeadingBullet(raw);
                    if (clean.EndsWith("시즌")) clean = clean.Substring(0, clean.Length - 2).Trim();
                    string norm = NormalizeTitleKey(clean);
                    if (norm.Length < 3) continue;
                    // Skip section/badge text & clipped "시즌" OCR noise (e.g. "『국l즌")
                    if (norm == "시즌" || norm == "긴급" || norm == "스토리" || norm == "진행중" || norm == "의뢰" ||
                        norm.Equals("new", StringComparison.OrdinalIgnoreCase)) continue;
                    string hangulOnly = Regex.Replace(clean, "[^가-힣]", "");
                    if (hangulOnly.Length < 2) continue;
                    if (hangulOnly.Length <= 3 && (hangulOnly.Contains("시즌") || Levenshtein(hangulOnly, "시즌") <= 1 || hangulOnly.EndsWith("즌"))) continue;
                    candidateLines.Add(clean);
                }

                // First pass: check if any candidate is unknown; if so, auto-learn the currently selected quest from the right pane!
                bool hasUnknown = false;
                for (int i = 0; i < candidateLines.Count; i++)
                {
                    if (MatchTitleTemplate(candidateLines[i]) == null) { hasUnknown = true; break; }
                }
                if (hasUnknown)
                {
                    await TryAutoLearnSelectedRightPaneAsync(full, videoW, videoH);
                }

                foreach (var clean in candidateLines)
                {
                    var tpl = MatchTitleTemplate(clean);
                    if (tpl != null)
                    {
                        result.Matched.Add(new QuestEntry
                        {
                            Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                            Title = tpl.Title,
                            Category = "",
                            Region = tpl.Region ?? "",
                            IsNest = tpl.IsNest,
                            IsAnyField = tpl.IsAnyField,
                            Species = tpl.Species ?? "",
                            TargetMonster = tpl.TargetMonster ?? "",
                            ActionType = tpl.ActionType ?? "기타",
                            WeaponType = tpl.WeaponType ?? "",
                            ObjectiveText = tpl.ObjectiveText ?? tpl.Title,
                            Progress = "",
                            RawFingerprint = (tpl.Title + "|" + tpl.Region + "|" + tpl.ObjectiveText).ToLowerInvariant()
                        });
                    }
                    else
                    {
                        result.UnknownTitles.Add(clean);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "목록 OCR 오류: " + ex.Message;
                return result;
            }
            finally
            {
                if (listBmp != null) listBmp.Dispose();
            }
        }

        public static async Task<BatchScanResult> ScanQuestListFromWindowAsync(IntPtr targetHwnd, int videoW, int videoH)
        {
            if (targetHwnd == IntPtr.Zero || IsIconic(targetHwnd))
                return new BatchScanResult { Matched = new List<QuestEntry>(), UnknownTitles = new List<string>(), ErrorMessage = "미러링 창이 열려 있지 않습니다." };

            RECT rc;
            if (!GetClientRect(targetHwnd, out rc))
                return new BatchScanResult { Matched = new List<QuestEntry>(), UnknownTitles = new List<string>(), ErrorMessage = "창 크기 조회 실패" };
            int cw = rc.Right - rc.Left;
            int ch = rc.Bottom - rc.Top;
            if (cw < 200 || ch < 120)
                return new BatchScanResult { Matched = new List<QuestEntry>(), UnknownTitles = new List<string>(), ErrorMessage = "창이 너무 작습니다." };

            using (var full = new Bitmap(cw, ch, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(full))
                {
                    IntPtr hdc = g.GetHdc();
                    try { PrintWindow(targetHwnd, hdc, 3); }
                    finally { g.ReleaseHdc(hdc); }
                }
                return await ScanQuestListFromBitmapAsync(full, videoW, videoH);
            }
        }

        private static async Task TryAutoLearnSelectedRightPaneAsync(Bitmap full, int videoW, int videoH)
        {
            Bitmap bodyBmp = null;
            Bitmap titleBmp = null;
            try
            {
                var crops = CropQuestRegionsFromBitmap(full, videoW, videoH);
                if (crops == null || crops.Item1 == null) return;
                bodyBmp = crops.Item1;
                titleBmp = crops.Item2;

                string[] bodyLines = await RunWinRtOcrAsync(bodyBmp);
                if (bodyLines == null || bodyLines.Length == 0) return;

                string extractedTitle = "";
                if (titleBmp != null)
                {
                    string[] titleLines = await RunWinRtOcrAsync(titleBmp);
                    if (titleLines != null && titleLines.Length > 0)
                        extractedTitle = CleanLeadingBullet(string.Join(" ", titleLines)).Trim();
                }

                var entry = ParseQuestLines(bodyLines, extractedTitle);
                if (entry != null)
                    LearnQuestTitle(entry);
            }
            catch { }
            finally
            {
                if (bodyBmp != null) bodyBmp.Dispose();
                if (titleBmp != null) titleBmp.Dispose();
            }
        }

        // LCS (Longest Common Subsequence) in-place sequence merge:
        // - Handles top, middle, and bottom new quest insertions seamlessly!
        // - Removes scroll overlap across any scroll offset (7+8 -> 10, etc.)!
        // - Preserves genuine duplicate quests held simultaneously!
        // - Safe against accidental double F7 presses (0 added)!
        public static int MergeBatchIntoExisting(List<QuestEntry> existing, List<QuestEntry> batch, int maxTotal)
        {
            if (existing == null || batch == null || batch.Count == 0) return 0;
            if (existing.Count == 0)
            {
                int added0 = 0;
                for (int i = 0; i < batch.Count && existing.Count < maxTotal; i++)
                {
                    existing.Add(batch[i]);
                    added0++;
                }
                return added0;
            }

            int m = existing.Count;
            int n = batch.Count;
            string[] ek = new string[m];
            string[] bk = new string[n];
            for (int i = 0; i < m; i++) ek[i] = NormalizeTitleKey(existing[i].Title);
            for (int j = 0; j < n; j++) bk[j] = NormalizeTitleKey(batch[j].Title);

            int[,] dp = new int[m + 1, n + 1];
            for (int i = m - 1; i >= 0; i--)
            {
                for (int j = n - 1; j >= 0; j--)
                {
                    if (ek[i] == bk[j])
                        dp[i, j] = dp[i + 1, j + 1] + 1;
                    else
                        dp[i, j] = dp[i + 1, j] >= dp[i, j + 1] ? dp[i + 1, j] : dp[i, j + 1];
                }
            }

            // Reconstruct matched index pairs (ei, bi) in ascending order
            var matchedPairs = new List<Tuple<int, int>>();
            int ci = 0, cj = 0;
            while (ci < m && cj < n)
            {
                if (ek[ci] == bk[cj])
                {
                    matchedPairs.Add(Tuple.Create(ci, cj));
                    ci++;
                    cj++;
                }
                else if (dp[ci + 1, cj] >= dp[ci, cj + 1])
                {
                    ci++;
                }
                else
                {
                    cj++;
                }
            }

            // Build merged list inserting unmatched batch items at their exact relative positions
            var merged = new List<QuestEntry>();
            int prevE = 0, prevB = 0;
            int addedCount = 0;
            int remainingCap = Math.Max(0, maxTotal - existing.Count);

            for (int p = 0; p < matchedPairs.Count; p++)
            {
                int matchE = matchedPairs[p].Item1;
                int matchB = matchedPairs[p].Item2;

                // Existing items before this anchor
                for (int e = prevE; e < matchE; e++)
                    merged.Add(existing[e]);

                // New batch items that appeared right before this anchor (top or middle insertion!)
                for (int b = prevB; b < matchB; b++)
                {
                    if (remainingCap > 0)
                    {
                        merged.Add(batch[b]);
                        addedCount++;
                        remainingCap--;
                    }
                }

                // The anchor itself (keep existing entry)
                merged.Add(existing[matchE]);
                prevE = matchE + 1;
                prevB = matchB + 1;
            }

            // Trailing existing items after last anchor
            for (int e = prevE; e < m; e++)
                merged.Add(existing[e]);

            // Trailing new batch items after last anchor (bottom scroll insertion!)
            for (int b = prevB; b < n; b++)
            {
                if (remainingCap > 0)
                {
                    merged.Add(batch[b]);
                    addedCount++;
                    remainingCap--;
                }
            }

            existing.Clear();
            existing.AddRange(merged);
            return addedCount;
        }

        public static List<QuestEntry> MergeBatchScannedQuests(List<QuestEntry> existing, List<QuestEntry> batch)
        {
            var copy = existing != null ? new List<QuestEntry>(existing) : new List<QuestEntry>();
            int before = copy.Count;
            MergeBatchIntoExisting(copy, batch, 100);
            var added = new List<QuestEntry>();
            // Legacy compatibility wrapper
            for (int i = before; i < copy.Count; i++) added.Add(copy[i]);
            return added;
        }

        public static string GetActiveQuestsPath()
        {
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(dir, "quests_active.json");
        }

        public static void SaveActiveQuests(List<QuestEntry> quests)
        {
            try
            {
                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                string json = ser.Serialize(quests ?? new List<QuestEntry>());
                File.WriteAllText(GetActiveQuestsPath(), json, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        public static List<QuestEntry> LoadActiveQuests()
        {
            try
            {
                string path = GetActiveQuestsPath();
                if (!File.Exists(path)) return new List<QuestEntry>();
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                var loaded = ser.Deserialize<List<QuestEntry>>(json);
                return loaded ?? new List<QuestEntry>();
            }
            catch
            {
                return new List<QuestEntry>();
            }
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
