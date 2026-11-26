using System.Windows;
using System.Windows.Markup;

namespace ScrcpyLauncher
{
    internal static class HuntLinkUiBuilder
    {
        public static UIElement Build()
        {
            string xaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      Background='#F5F1E8'>
  <Grid.Resources>
    <!-- Concept B: Outlanders Parchment & Forest Modern Light Theme -->
    <Style TargetType='Border' x:Key='Card'>
      <Setter Property='Background' Value='#FFFDF8'/>
      <Setter Property='BorderBrush' Value='#DDD4C3'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='12'/>
      <Setter Property='Padding' Value='15,12'/>
      <Setter Property='Margin' Value='0,0,0,10'/>
      <Setter Property='Effect'>
        <Setter.Value>
          <DropShadowEffect BlurRadius='10' ShadowDepth='2' Direction='270' Color='#3D3220' Opacity='0.06'/>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='Border' x:Key='StepBadge'>
      <Setter Property='Background' Value='#E6EFE9'/>
      <Setter Property='BorderBrush' Value='#B8D0C0'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='10'/>
      <Setter Property='Width' Value='20'/>
      <Setter Property='Height' Value='20'/>
      <Setter Property='Margin' Value='0,0,8,0'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style TargetType='TextBlock' x:Key='StepNum'>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Foreground' Value='#2C5E3F'/>
      <Setter Property='HorizontalAlignment' Value='Center'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style TargetType='TextBlock' x:Key='SectionTitle'>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Foreground' Value='#1F2922'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>
    <Style TargetType='TextBlock' x:Key='FieldLabel'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#5C5548'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='Margin' Value='0,0,8,0'/>
    </Style>

    <!-- Sidebar Navigation RadioButton -->
    <Style TargetType='RadioButton' x:Key='NavItem'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Height' Value='42'/>
      <Setter Property='Margin' Value='10,3,10,3'/>
      <Setter Property='Foreground' Value='#3F3A31'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='RadioButton'>
            <Border x:Name='bg' Background='Transparent' CornerRadius='10' Padding='12,0'>
              <ContentPresenter VerticalAlignment='Center' HorizontalAlignment='Left'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bg' Property='Background' Value='#E3DAC8'/>
              </Trigger>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='bg' Property='Background' Value='#2C5E3F'/>
                <Setter Property='Foreground' Value='#FFFDF8'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Large Mode Selector Card (Concept B) -->
    <Style TargetType='RadioButton' x:Key='ModeCard'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='RadioButton'>
            <Border x:Name='bd' Background='#FFFDF8' BorderBrush='#DDD4C3' BorderThickness='1.5' CornerRadius='12' Padding='12,14'>
              <Grid>
                <Border x:Name='chkBadge' HorizontalAlignment='Right' VerticalAlignment='Top' Width='20' Height='20' CornerRadius='10' Background='#B8832A' Visibility='Collapsed'>
                  <TextBlock Text='✓' FontSize='11' FontWeight='Bold' Foreground='#FFFFFF' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                </Border>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
              </Grid>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bd' Property='BorderBrush' Value='#7DA18A'/>
                <Setter TargetName='bd' Property='Background' Value='#FBF8F1'/>
              </Trigger>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='bd' Property='Background' Value='#F3F8F5'/>
                <Setter TargetName='bd' Property='BorderBrush' Value='#2C5E3F'/>
                <Setter TargetName='bd' Property='BorderThickness' Value='2'/>
                <Setter TargetName='chkBadge' Property='Visibility' Value='Visible'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- One-Click Visual Preset Pill -->
    <Style TargetType='Button' x:Key='PresetPill'>
      <Setter Property='Height' Value='32'/>
      <Setter Property='Padding' Value='14,4'/>
      <Setter Property='Margin' Value='0,0,8,0'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Background' Value='#FFFDF8'/>
      <Setter Property='Foreground' Value='#2D2A24'/>
      <Setter Property='BorderBrush' Value='#D4C9B4'/>
      <Setter Property='BorderThickness' Value='1.5'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='16' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bd' Property='BorderBrush' Value='#2C5E3F'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- Standard Inputs -->
    <Style TargetType='TextBox'>
      <Setter Property='Height' Value='30'/>
      <Setter Property='Padding' Value='8,4'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Background' Value='#FFFFFF'/>
      <Setter Property='Foreground' Value='#1F2421'/>
      <Setter Property='BorderBrush' Value='#D4C9B4'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TextBox'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'>
              <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsKeyboardFocused' Value='True'>
                <Setter TargetName='bd' Property='BorderBrush' Value='#2C5E3F'/>
                <Setter TargetName='bd' Property='BorderThickness' Value='1.5'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='ComboBox'>
      <Setter Property='Height' Value='30'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Background' Value='#FFFFFF'/>
      <Setter Property='Foreground' Value='#1F2421'/>
      <Setter Property='BorderBrush' Value='#D4C9B4'/>
    </Style>
    <Style TargetType='CheckBox'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#2C2822'/>
      <Setter Property='Margin' Value='0,0,14,6'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Cursor' Value='Hand'/>
    </Style>

    <!-- Buttons -->
    <Style TargetType='Button' x:Key='SubBtn'>
      <Setter Property='Height' Value='30'/>
      <Setter Property='Padding' Value='11,4'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='FontWeight' Value='Medium'/>
      <Setter Property='Background' Value='#FFFDF8'/>
      <Setter Property='Foreground' Value='#2C2822'/>
      <Setter Property='BorderBrush' Value='#D4C9B4'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bd' Property='Background' Value='#F3ECE0'/>
                <Setter TargetName='bd' Property='BorderBrush' Value='#B8AD96'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='bd' Property='Opacity' Value='0.45'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='Button' x:Key='SoftGreenBtn' BasedOn='{StaticResource SubBtn}'>
      <Setter Property='Background' Value='#E6EFE9'/>
      <Setter Property='Foreground' Value='#1E462E'/>
      <Setter Property='BorderBrush' Value='#B8D0C0'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>
    <Style TargetType='Button' x:Key='SoftGoldBtn' BasedOn='{StaticResource SubBtn}'>
      <Setter Property='Background' Value='#FDF6E7'/>
      <Setter Property='Foreground' Value='#8C5E14'/>
      <Setter Property='BorderBrush' Value='#E6C98B'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>
    <Style TargetType='Button' x:Key='SoftRedBtn' BasedOn='{StaticResource SubBtn}'>
      <Setter Property='Background' Value='#FEF2F2'/>
      <Setter Property='Foreground' Value='#B91C1C'/>
      <Setter Property='BorderBrush' Value='#FECACA'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>
    <Style TargetType='Button' x:Key='PrimaryBtn'>
      <Setter Property='Height' Value='42'/>
      <Setter Property='Padding' Value='24,8'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='FontWeight' Value='Bold'/>
      <Setter Property='Background' Value='#2C5E3F'/>
      <Setter Property='Foreground' Value='#FFFDF8'/>
      <Setter Property='BorderBrush' Value='#B8832A'/>
      <Setter Property='BorderThickness' Value='1.5'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='10' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bd' Property='Background' Value='#234B32'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='bd' Property='Background' Value='#1A3A26'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='bd' Property='Background' Value='#A6B5AC'/>
                <Setter TargetName='bd' Property='BorderBrush' Value='#A6B5AC'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType='Button' x:Key='DangerBtn' BasedOn='{StaticResource PrimaryBtn}'>
      <Setter Property='Background' Value='#B91C1C'/>
      <Setter Property='BorderBrush' Value='#991B1B'/>
      <Setter Property='Padding' Value='16,8'/>
    </Style>
    <Style TargetType='Button' x:Key='ActionSubBtn' BasedOn='{StaticResource SubBtn}'>
      <Setter Property='Height' Value='42'/>
      <Setter Property='Padding' Value='13,6'/>
      <Setter Property='FontSize' Value='12.5'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>
    <Style TargetType='Button' x:Key='DarkGhostBtn'>
      <Setter Property='Height' Value='24'/>
      <Setter Property='Padding' Value='8,2'/>
      <Setter Property='FontSize' Value='11'/>
      <Setter Property='Background' Value='#26332B'/>
      <Setter Property='Foreground' Value='#E6EFE9'/>
      <Setter Property='BorderBrush' Value='#3E5246'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='5' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='bd' Property='Background' Value='#34463B'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Grid.Resources>

  <Grid.ColumnDefinitions>
    <ColumnDefinition Width='176'/>
    <ColumnDefinition Width='*'/>
  </Grid.ColumnDefinitions>

  <!-- LEFT SIDEBAR (Concept B Parchment Sand) -->
  <Border Grid.Column='0' Background='#ECE5D5' BorderBrush='#DDD4C3' BorderThickness='0,0,1,0'>
    <DockPanel LastChildFill='False'>
      <!-- Brand Header -->
      <StackPanel DockPanel.Dock='Top' Margin='14,18,14,16'>
        <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
          <Border Width='28' Height='28' CornerRadius='7' Background='#2C5E3F' BorderBrush='#B8832A' BorderThickness='1.5' Margin='0,0,8,0'>
            <TextBlock Text='⚔' FontSize='14' Foreground='#F5D996' HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <StackPanel VerticalAlignment='Center'>
            <StackPanel Orientation='Horizontal'>
              <TextBlock Text='HuntLink' FontSize='17' FontWeight='Bold' Foreground='#1F2922'/>
              <Border Background='#FDF6E7' BorderBrush='#E0C284' BorderThickness='1' CornerRadius='6' Padding='5,1' Margin='5,0,0,0' VerticalAlignment='Center'>
                <TextBlock Text='v2.0' FontSize='10' FontWeight='Bold' Foreground='#8C5E14'/>
              </Border>
            </StackPanel>
            <TextBlock Text='MHO PC Companion' FontSize='10' Foreground='#7A7060' Margin='1,-1,0,0'/>
          </StackPanel>
        </StackPanel>
      </StackPanel>

      <!-- 5 Navigation Items -->
      <StackPanel DockPanel.Dock='Top'>
        <RadioButton x:Name='navHome' GroupName='MainNav' Style='{StaticResource NavItem}' IsChecked='True'>
          <StackPanel Orientation='Horizontal'>
            <TextBlock Text='🏠' Margin='0,0,9,0' FontSize='14'/>
            <TextBlock x:Name='txtNavHome' Text='홈 · 실행'/>
          </StackPanel>
        </RadioButton>
        <RadioButton x:Name='navKeymap' GroupName='MainNav' Style='{StaticResource NavItem}'>
          <StackPanel Orientation='Horizontal'>
            <TextBlock Text='🎮' Margin='0,0,9,0' FontSize='14'/>
            <TextBlock x:Name='txtNavKeymap' Text='키 매핑'/>
          </StackPanel>
        </RadioButton>
        <RadioButton x:Name='navQuest' GroupName='MainNav' Style='{StaticResource NavItem}'>
          <StackPanel Orientation='Horizontal'>
            <TextBlock Text='📋' Margin='0,0,9,0' FontSize='14'/>
            <TextBlock x:Name='txtNavQuest' Text='퀘스트 플래너'/>
          </StackPanel>
        </RadioButton>
        <RadioButton x:Name='navCodex' GroupName='MainNav' Style='{StaticResource NavItem}'>
          <StackPanel Orientation='Horizontal'>
            <TextBlock Text='📖' Margin='0,0,9,0' FontSize='14'/>
            <TextBlock x:Name='txtNavCodex' Text='몬스터 도감'/>
          </StackPanel>
        </RadioButton>
        <RadioButton x:Name='navAdvanced' GroupName='MainNav' Style='{StaticResource NavItem}'>
          <StackPanel Orientation='Horizontal'>
            <TextBlock Text='⚙️' Margin='0,0,9,0' FontSize='14'/>
            <TextBlock x:Name='txtNavAdvanced' Text='고급 설정'/>
          </StackPanel>
        </RadioButton>
      </StackPanel>

      <!-- Sidebar Footer: Update + scrcpy status + Language -->
      <StackPanel DockPanel.Dock='Bottom' Margin='12,0,12,14'>
        <Button x:Name='btnUpdate' Content='새 버전' Style='{StaticResource SoftGoldBtn}' Height='28' FontSize='11.5' Margin='0,0,0,7' Visibility='Collapsed'/>
        <Border x:Name='bdScrcpyStatus' Background='#E6EFE9' BorderBrush='#C8DBC9' BorderThickness='1' CornerRadius='8' Padding='8,5' Margin='0,0,0,7'>
          <TextBlock x:Name='txtScrcpyStatus' Text='scrcpy 확인 중...' FontSize='11' FontWeight='SemiBold' Foreground='#1E462E' TextTrimming='CharacterEllipsis' HorizontalAlignment='Center'/>
        </Border>
        <Button x:Name='btnDownloadScrcpy' Content='scrcpy 다운로드' Style='{StaticResource SoftRedBtn}' Height='26' FontSize='11' Margin='0,0,0,7' Visibility='Collapsed'/>
        <ComboBox x:Name='cmbLanguage' Height='26' FontSize='11'>
          <ComboBoxItem Content='한국어' Tag='ko'/>
          <ComboBoxItem Content='English' Tag='en'/>
          <ComboBoxItem Content='日本語' Tag='ja'/>
        </ComboBox>
      </StackPanel>
    </DockPanel>
  </Border>

  <!-- RIGHT MAIN CONTENT AREA -->
  <Grid Grid.Column='1' Margin='18,14,18,14'>
    <Grid.RowDefinitions>
      <RowDefinition Height='*'/>
      <RowDefinition Height='Auto'/>
      <RowDefinition Height='Auto'/>
      <RowDefinition Height='Auto'/>
    </Grid.RowDefinitions>

    <!-- PAGE CONTAINER -->
    <Grid Grid.Row='0'>
      <!-- 1. HOME & PLAY PAGE (Concept B Signature Screen) -->
      <ScrollViewer x:Name='pageHome' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
        <StackPanel>
          <!-- Device Connection Bar -->
          <Border Style='{StaticResource Card}' Padding='12,10'>
            <StackPanel>
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <ComboBox x:Name='cmbDevices' Grid.Column='0' Margin='0,0,7,0'/>
                <Button x:Name='btnRefreshDevices' Grid.Column='1' Content='새로고침' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnUsbToWifi' Grid.Column='2' Content='유선 → 무선' Style='{StaticResource SoftGreenBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnDisconnectWifi' Grid.Column='3' Content='무선 해제' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnRestartAdb' Grid.Column='4' Content='ADB 재시작' Style='{StaticResource SoftGoldBtn}'/>
              </Grid>
              <DockPanel x:Name='pnlConnStatus' Margin='0,7,0,0' Visibility='Collapsed'>
                <Border x:Name='bdConnBadge' DockPanel.Dock='Left' Background='#E6EFE9' BorderBrush='#B8D0C0' BorderThickness='1' CornerRadius='10' Padding='9,2' Margin='0,0,6,0' VerticalAlignment='Center'>
                  <TextBlock x:Name='txtConnBadge' Text='' FontSize='11' FontWeight='Bold' Foreground='#1E462E'/>
                </Border>
                <Border x:Name='bdBattBadge' DockPanel.Dock='Left' Background='#E6EFE9' BorderBrush='#B8D0C0' BorderThickness='1' CornerRadius='10' Padding='9,2' Margin='0,0,8,0' VerticalAlignment='Center' Visibility='Collapsed'>
                  <TextBlock x:Name='txtBattBadge' Text='' FontSize='11' FontWeight='Bold' Foreground='#1E462E'/>
                </Border>
                <TextBlock x:Name='txtConnDetail' Text='' FontSize='11' Foreground='#6B6457' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
              </DockPanel>
            </StackPanel>
          </Border>

          <!-- Control Mode 3 Big Cards -->
          <UniformGrid Columns='3' Margin='0,2,0,6'>
            <RadioButton x:Name='rbModeKeymap' GroupName='CtrlMode' Style='{StaticResource ModeCard}' Margin='0,0,7,0' IsChecked='True'>
              <StackPanel HorizontalAlignment='Center'>
                <TextBlock Text='⌨️🖱️' FontSize='24' HorizontalAlignment='Center' Margin='0,0,0,6'/>
                <TextBlock x:Name='txtModeKmTitle' Text='키보드 + 마우스' FontSize='13.5' FontWeight='Bold' Foreground='#1F2922' HorizontalAlignment='Center'/>
                <TextBlock x:Name='txtModeKmSub' Text='WASD · 시점 회전 · 스킬 매핑' FontSize='11' Foreground='#6B6457' HorizontalAlignment='Center' Margin='0,3,0,0'/>
              </StackPanel>
            </RadioButton>
            <RadioButton x:Name='rbModeGamepad' GroupName='CtrlMode' Style='{StaticResource ModeCard}' Margin='4,0,4,0'>
              <StackPanel HorizontalAlignment='Center'>
                <TextBlock Text='🎮' FontSize='24' HorizontalAlignment='Center' Margin='0,0,0,6'/>
                <TextBlock x:Name='txtModeGpTitle' Text='게임패드 (Xbox / PS)' FontSize='13.5' FontWeight='Bold' Foreground='#1F2922' HorizontalAlignment='Center'/>
                <TextBlock x:Name='txtModeGpSub' Text='물리 컨트롤러 · 보조 키 지원' FontSize='11' Foreground='#6B6457' HorizontalAlignment='Center' Margin='0,3,0,0'/>
              </StackPanel>
            </RadioButton>
            <RadioButton x:Name='rbModeNormal' GroupName='CtrlMode' Style='{StaticResource ModeCard}' Margin='7,0,0,0'>
              <StackPanel HorizontalAlignment='Center'>
                <TextBlock Text='📱' FontSize='24' HorizontalAlignment='Center' Margin='0,0,0,6'/>
                <TextBlock x:Name='txtModeNmTitle' Text='일반 미러링' FontSize='13.5' FontWeight='Bold' Foreground='#1F2922' HorizontalAlignment='Center'/>
                <TextBlock x:Name='txtModeNmSub' Text='일반 터치 · 마우스 · 영상 시청' FontSize='11' Foreground='#6B6457' HorizontalAlignment='Center' Margin='0,3,0,0'/>
              </StackPanel>
            </RadioButton>
          </UniformGrid>
          <TextBlock x:Name='txtModeSummary' Text='' FontSize='11.5' Foreground='#2C5E3F' FontWeight='SemiBold' Margin='4,0,0,12'/>

          <!-- One-Click Visual Presets (5 Pills) -->
          <TextBlock x:Name='lblPresetsTitle' Text='화면 · 성능 원클릭 프리셋' FontSize='12' FontWeight='Bold' Foreground='#5C5548' Margin='4,0,0,6'/>
          <WrapPanel Margin='0,0,0,12'>
            <Button x:Name='btnPreset1080' Content='추천 1080p' Style='{StaticResource PresetPill}'/>
            <Button x:Name='btnPreset1440' Content='고화질 1440p' Style='{StaticResource PresetPill}'/>
            <Button x:Name='btnPresetMonitor' Content='내 모니터 맞춤' Style='{StaticResource PresetPill}'/>
            <Button x:Name='btnPresetNative' Content='기기 본체 화면' Style='{StaticResource PresetPill}'/>
            <Button x:Name='btnPresetWifi' Content='무선 안정' Style='{StaticResource PresetPill}'/>
          </WrapPanel>

          <!-- Home Mini Quest Planner Summary Card -->
          <Border x:Name='bdHomeQuestCard' Style='{StaticResource Card}' Cursor='Hand'>
            <DockPanel>
              <Border DockPanel.Dock='Left' Width='36' Height='36' CornerRadius='9' Background='#FDF6E7' BorderBrush='#E6C98B' BorderThickness='1' Margin='0,0,12,0' VerticalAlignment='Center'>
                <TextBlock Text='📜' FontSize='17' HorizontalAlignment='Center' VerticalAlignment='Center'/>
              </Border>
              <Button x:Name='btnGoQuestTab' DockPanel.Dock='Right' Content='플래너 열기 →' Style='{StaticResource SoftGreenBtn}' Height='28' VerticalAlignment='Center' Margin='10,0,0,0'/>
              <StackPanel VerticalAlignment='Center'>
                <StackPanel Orientation='Horizontal'>
                  <TextBlock x:Name='lblHomeQuestTitle' Text='긴급 퀘스트 플래너' FontSize='13.5' FontWeight='Bold' Foreground='#1F2922'/>
                  <TextBlock x:Name='txtHomeQuestMeta' Text='  ·  오늘 완료: 0건  ·  보유: 0 / 10' FontSize='11.5' FontWeight='SemiBold' Foreground='#2C5E3F' VerticalAlignment='Center'/>
                </StackPanel>
                <TextBlock x:Name='txtHomeQuestRec' Text='게임에서 [F7] 목록 일괄 스캔 또는 [F8] 개별 수집으로 추천 조합을 확인하세요.' FontSize='11.5' Foreground='#5C5548' Margin='0,3,0,0' TextTrimming='CharacterEllipsis'/>
              </StackPanel>
            </DockPanel>
          </Border>

          <!-- Quick Toggles on Home (Always on top & Fullscreen) -->
          <Border Style='{StaticResource Card}' Padding='14,10'>
            <StackPanel>
              <TextBlock x:Name='lblQuickOpts' Text='자주 쓰는 빠른 옵션 (세부 설정은 좌측 [고급 설정] 탭)' FontSize='11.5' FontWeight='SemiBold' Foreground='#6B6457' Margin='0,0,0,7'/>
              <WrapPanel>
                <CheckBox x:Name='chkTurnScreenOff' Content='폰 화면 끄기 (-S)'/>
                <CheckBox x:Name='chkFullscreen' Content='전체화면 시작 (-f)'/>
                <CheckBox x:Name='chkAlwaysOnTop' Content='항상 위에 (실시간)'/>
                <CheckBox x:Name='chkRememberWin' Content='창 위치·크기 기억'/>
                <CheckBox x:Name='chkAudioSafe' Content='오디오 안정화'/>
                <CheckBox x:Name='chkAutoMuteBg' Content='다른 창 이동 시 음소거'/>
              </WrapPanel>
            </StackPanel>
          </Border>
        </StackPanel>
      </ScrollViewer>

      <!-- 2. KEY MAPPING PAGE -->
      <ScrollViewer x:Name='pageKeymap' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' Visibility='Collapsed'>
        <StackPanel>
          <Border x:Name='bdKmModeWarning' Background='#FDF6E7' BorderBrush='#E6C98B' BorderThickness='1' CornerRadius='10' Padding='12,8' Margin='0,0,0,10' Visibility='Collapsed'>
            <StackPanel>
              <TextBlock x:Name='txtKmModeWarning' Text='현재 키보드+마우스 모드가 아닙니다.' FontSize='11.5' Foreground='#8C5E14' TextWrapping='Wrap'/>
              <CheckBox x:Name='chkGamepadKbAssist' Content='게임패드 모드에서도 키보드 보조 매핑 사용 (실시간 적용 · WASD/마우스 제외)' FontSize='11.5' FontWeight='SemiBold' Foreground='#1E462E' Margin='0,6,0,0'/>
            </StackPanel>
          </Border>

          <Border Style='{StaticResource Card}'>
            <StackPanel>
              <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='1' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='txtKmSec1' Text='키 매핑 프로파일 (무기별 관리 · LD플레이어 .kmp 호환)' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
              <Grid Margin='0,0,0,7'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <ComboBox x:Name='cmbKeyProfiles' Grid.Column='0' Margin='0,0,6,0'/>
                <Button x:Name='btnReloadProfiles' Grid.Column='1' Content='새로고침' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnImportKmp' Grid.Column='2' Content='LD .kmp 불러오기' Style='{StaticResource SoftGreenBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnSaveProfile' Grid.Column='3' Content='저장' Style='{StaticResource SubBtn}'/>
              </Grid>
              <WrapPanel>
                <Button x:Name='btnSaveAsProfile' Content='다른 이름으로 저장' Style='{StaticResource SubBtn}' Height='26' Padding='10,2' FontSize='11.5' Margin='0,0,5,0'/>
                <Button x:Name='btnRenameProfile' Content='이름 변경' Style='{StaticResource SubBtn}' Height='26' Padding='10,2' FontSize='11.5' Margin='0,0,5,0'/>
                <Button x:Name='btnDeleteProfile' Content='프로파일 삭제' Style='{StaticResource SoftRedBtn}' Height='26' Padding='10,2' FontSize='11.5' Margin='0,0,8,0'/>
                <Button x:Name='btnExportCode' Content='코드 복사' Style='{StaticResource SoftGoldBtn}' Height='26' Padding='10,2' FontSize='11.5' Margin='0,0,5,0'/>
                <Button x:Name='btnImportCode' Content='코드 불러오기' Style='{StaticResource SoftGoldBtn}' Height='26' Padding='10,2' FontSize='11.5' Margin='0,0,8,0'/>
                <TextBlock x:Name='lblHotkeyHint' Text='게임 중 Ctrl+F1~F9 전환' FontSize='11' Foreground='#6B6457' VerticalAlignment='Center'/>
              </WrapPanel>
            </StackPanel>
          </Border>

          <Border Style='{StaticResource Card}'>
            <StackPanel>
              <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='2' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='txtKmSec2' Text='멀티터치 조작 · 시점 회전 설정' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
              <WrapPanel Margin='2,0,0,8'>
                <CheckBox x:Name='chkKmJoy' Content='WASD + Shift 질주'/>
                <CheckBox x:Name='chkKmCam' Content='마우스 시점 회전'/>
                <CheckBox x:Name='chkKmLeft' Content='좌클릭 공격'/>
                <CheckBox x:Name='chkKmRight' Content='우클릭 보조'/>
                <CheckBox x:Name='chkKmRightWheel' Content='우클릭 휠드래그'/>
                <CheckBox x:Name='chkKmHints' Content='화면 키 힌트' IsChecked='True'/>
              </WrapPanel>
              <Grid Margin='0,0,0,9'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='92'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='36'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='36'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblCamToggle' Grid.Column='0' Text='시점 토글' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbKmToggleKey' Grid.Column='1' Margin='0,0,10,0'>
                  <ComboBoxItem Content='Alt' Tag='18'/>
                  <ComboBoxItem Content='` (~)' Tag='192'/>
                  <ComboBoxItem Content='Tab' Tag='9'/>
                </ComboBox>
                <TextBlock x:Name='lblSensX' Grid.Column='2' Text='감도 X' Style='{StaticResource FieldLabel}'/>
                <Slider x:Name='sldKmSensX' Grid.Column='3' Minimum='0.1' Maximum='3.0' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
                <TextBlock x:Name='txtKmSensXVal' Grid.Column='4' Text='1.00' FontSize='11.5' FontWeight='SemiBold' Foreground='#1F2922' VerticalAlignment='Center' Margin='0,0,8,0'/>
                <TextBlock x:Name='lblSensY' Grid.Column='5' Text='감도 Y' Style='{StaticResource FieldLabel}'/>
                <Slider x:Name='sldKmSensY' Grid.Column='6' Minimum='0.1' Maximum='3.0' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
                <TextBlock x:Name='txtKmSensYVal' Grid.Column='7' Text='1.00' FontSize='11.5' FontWeight='SemiBold' Foreground='#1F2922' VerticalAlignment='Center'/>
              </Grid>
              <Grid Margin='0,0,0,10'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='40'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='40'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblHintOp' Grid.Column='0' Text='힌트 투명도' Style='{StaticResource FieldLabel}'/>
                <Slider x:Name='sldHintOpacity' Grid.Column='1' Minimum='0.25' Maximum='1.0' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
                <TextBlock x:Name='txtHintOpacityVal' Grid.Column='2' Text='100%' FontSize='11.5' FontWeight='SemiBold' Foreground='#1F2922' VerticalAlignment='Center' Margin='0,0,10,0'/>
                <TextBlock x:Name='lblHintSc' Grid.Column='3' Text='힌트 크기' Style='{StaticResource FieldLabel}'/>
                <Slider x:Name='sldHintScale' Grid.Column='4' Minimum='0.7' Maximum='1.5' Value='1.0' TickFrequency='0.05' IsSnapToTickEnabled='True' VerticalAlignment='Center' Margin='0,0,4,0'/>
                <TextBlock x:Name='txtHintScaleVal' Grid.Column='5' Text='100%' FontSize='11.5' FontWeight='SemiBold' Foreground='#1F2922' VerticalAlignment='Center'/>
              </Grid>
              <StackPanel Orientation='Horizontal'>
                <Button x:Name='btnKmEditOverlay' Content='화면에서 키 배치' Style='{StaticResource SoftGreenBtn}' Height='32' Padding='16,5' Margin='0,0,8,0'/>
                <Button x:Name='btnKmStart' Content='수동 재연결' Style='{StaticResource SubBtn}' Height='32' Padding='12,5' Margin='0,0,6,0'/>
                <Button x:Name='btnKmStop' Content='매핑 일시중지' Style='{StaticResource SoftRedBtn}' Height='32' Padding='12,5' IsEnabled='False'/>
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Style='{StaticResource Card}'>
            <StackPanel>
              <StackPanel Orientation='Horizontal' Margin='0,0,0,8'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='3' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='txtKmSec3' Text='매핑된 액션 키 목록 (누름/홀드 멀티터치 지원)' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
              <ListBox x:Name='lstKmKeys' Height='110' BorderBrush='#D4C9B4' BorderThickness='1' Background='#FFFFFF' FontSize='12' Margin='0,0,0,8'/>
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='115'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name='txtKmNewKey' Grid.Column='0' IsReadOnly='True' Text='클릭 후 키 누름' Foreground='#6B6457' Margin='0,0,6,0'/>
                <TextBox x:Name='txtKmNewLabel' Grid.Column='1' Margin='0,0,8,0'/>
                <CheckBox x:Name='chkKmNewWheel' Grid.Column='2' Content='휠 드래그' VerticalAlignment='Center' Margin='0,0,8,0'/>
                <Button x:Name='btnKmAddKey' Grid.Column='3' Content='+ 키 추가' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnKmDelKey' Grid.Column='4' Content='선택 삭제' Style='{StaticResource SoftRedBtn}'/>
              </Grid>
            </StackPanel>
          </Border>
        </StackPanel>
      </ScrollViewer>

      <!-- 3. QUEST PLANNER PAGE -->
      <Grid x:Name='pageQuest' Visibility='Collapsed'>
        <Grid.RowDefinitions>
          <RowDefinition Height='Auto'/>
          <RowDefinition Height='*'/>
        </Grid.RowDefinitions>
        <Border Grid.Row='0' Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,8'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='1' Style='{StaticResource StepNum}'/></Border>
              <TextBlock x:Name='lblQuestSec1' Text='긴급 퀘스트 수집 ([F7] 왼쪽 목록 일괄 스캔 · [F8] 개별 수집 및 자동 학습)' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <Grid Margin='0,0,0,8'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <Button x:Name='btnQuestBatchScan' Grid.Column='0' Content='⚡ 목록 일괄 (F7)' Style='{StaticResource SoftGoldBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnQuestScanOne' Grid.Column='1' Content='+ 개별 (F8)' Style='{StaticResource SoftGreenBtn}' Margin='0,0,5,0'/>
              <Button x:Name='btnQuestUndo' Grid.Column='2' Content='↩ 취소' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
              <Border x:Name='bdQuestDaily' Grid.Column='4' Background='#E6EFE9' BorderBrush='#B8D0C0' BorderThickness='1' CornerRadius='7' Padding='8,4' Margin='0,0,4,0' VerticalAlignment='Center' ToolTip='매일 한국시간 오전 5시에 자동 초기화됩니다.'>
                <TextBlock x:Name='txtQuestDaily' Text='오늘 완료: 0건' FontSize='11.5' FontWeight='Bold' Foreground='#1E462E'/>
              </Border>
              <Button x:Name='btnQuestResetDaily' Grid.Column='5' Content='리셋' Style='{StaticResource SubBtn}' Padding='7,4' Margin='0,0,6,0'/>
              <Border Grid.Column='6' Background='#FDF6E7' BorderBrush='#E6C98B' BorderThickness='1' CornerRadius='7' Padding='8,4' Margin='0,0,5,0' VerticalAlignment='Center'>
                <TextBlock x:Name='txtQuestCount' Text='보유: 0 / 10' FontSize='11.5' FontWeight='Bold' Foreground='#8C5E14'/>
              </Border>
              <Button x:Name='btnQuestClear' Grid.Column='7' Content='비우기' Style='{StaticResource SoftRedBtn}' Padding='9,4'/>
            </Grid>
            <Border Background='#F3F8F5' BorderBrush='#B8D0C0' BorderThickness='1' CornerRadius='8' Padding='10,6'>
              <TextBlock x:Name='txtQuestBanner' Text='게임에서 퀘스트 창을 열고 [F7] 목록 일괄 스캔 또는 [F8] 개별 수집을 누르세요.' FontSize='12' FontWeight='SemiBold' Foreground='#1E462E' TextWrapping='Wrap'/>
            </Border>
          </StackPanel>
        </Border>

        <Border Grid.Row='1' Style='{StaticResource Card}' Margin='0'>
          <Grid>
            <Grid.RowDefinitions>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='*'/>
            </Grid.RowDefinitions>
            <DockPanel Grid.Row='0' Margin='0,0,0,8'>
              <CheckBox x:Name='chkQuestNestFirst' DockPanel.Dock='Right' Content='둥지 퀘스트 먼저 보기' VerticalAlignment='Center' Margin='8,0,0,0' FontWeight='SemiBold' Foreground='#2C5E3F'/>
              <StackPanel Orientation='Horizontal'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='2' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='lblQuestSec2' Text='동시 클리어 추천 조합 (클리어한 조합의 [✓ 완료]를 눌러 비우세요)' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
            </DockPanel>
            <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'>
              <StackPanel x:Name='pnlQuestPlans'/>
            </ScrollViewer>
          </Grid>
        </Border>
      </Grid>

      <!-- 4. MONSTER CODEX PAGE (NEW Dedicated Tab!) -->
      <Grid x:Name='pageCodex' Visibility='Collapsed'>
        <Grid.RowDefinitions>
          <RowDefinition Height='Auto'/>
          <RowDefinition Height='*'/>
        </Grid.RowDefinitions>
        <Border Grid.Row='0' Style='{StaticResource Card}'>
          <StackPanel>
            <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
              <Border Style='{StaticResource StepBadge}'><TextBlock Text='📖' FontSize='10' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
              <TextBlock x:Name='lblCodexTitle' Text='몬스터 도감 · 서식지 빠른 검색 (39종 · 4개 위키 교차 검증 DB)' Style='{StaticResource SectionTitle}'/>
            </StackPanel>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='140'/>
                <ColumnDefinition Width='130'/>
              </Grid.ColumnDefinitions>
              <Grid Grid.Column='0' Margin='0,0,8,0'>
                <TextBox x:Name='txtCodexSearch'/>
                <TextBlock x:Name='txtCodexSearchHint' Text='몬스터 이름 검색 (예: 안쟈나프, 디아블로스, 융광종...)' FontSize='11.5' Foreground='#8F8677' IsHitTestVisible='False' VerticalAlignment='Center' Margin='9,0,0,0'/>
              </Grid>
              <ComboBox x:Name='cmbCodexRegion' Grid.Column='1' Margin='0,0,8,0'>
                <ComboBoxItem Content='전체 지역' Tag='' IsSelected='True'/>
                <ComboBoxItem Content='거목의 벌판' Tag='거목의 벌판'/>
                <ComboBoxItem Content='열사 평원' Tag='열사 평원'/>
                <ComboBoxItem Content='부패의 심연' Tag='부패의 심연'/>
                <ComboBoxItem Content='찬란한 군도' Tag='찬란한 군도'/>
              </ComboBox>
              <ComboBox x:Name='cmbCodexSpecies' Grid.Column='2'>
                <ComboBoxItem Content='전체 종족' Tag='' IsSelected='True'/>
                <ComboBoxItem Content='아룡종' Tag='아룡종'/>
                <ComboBoxItem Content='조룡종' Tag='조룡종'/>
                <ComboBoxItem Content='어룡종' Tag='어룡종'/>
                <ComboBoxItem Content='수룡종' Tag='수룡종'/>
                <ComboBoxItem Content='비룡종' Tag='비룡종'/>
                <ComboBoxItem Content='고룡종' Tag='고룡종'/>
                <ComboBoxItem Content='융광종만' Tag='융광종'/>
              </ComboBox>
            </Grid>
          </StackPanel>
        </Border>
        <Border Grid.Row='1' Style='{StaticResource Card}' Margin='0'>
          <ScrollViewer VerticalScrollBarVisibility='Auto'>
            <StackPanel x:Name='pnlCodexList'/>
          </ScrollViewer>
        </Border>
      </Grid>

      <!-- 5. ADVANCED SETTINGS PAGE -->
      <ScrollViewer x:Name='pageAdvanced' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' Visibility='Collapsed'>
        <StackPanel>
          <!-- Video & Performance -->
          <Border Style='{StaticResource Card}'>
            <StackPanel>
              <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='1' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='txtSec2Title' Text='영상 · 성능 세부 설정' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
              <Grid Margin='0,0,0,9'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='120'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='54'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='100'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='82'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblCodec' Grid.Column='0' Text='코덱' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbCodec' Grid.Column='1' Margin='0,0,8,0'>
                  <ComboBoxItem Content='h264 (안정/저지연)' Tag='h264'/>
                  <ComboBoxItem Content='h265 (고화질)' Tag='h265'/>
                  <ComboBoxItem Content='av1' Tag='av1'/>
                </ComboBox>
                <TextBlock Grid.Column='2' Text='Mbps' Style='{StaticResource FieldLabel}'/>
                <TextBox x:Name='txtBitrate' Grid.Column='3' Margin='0,0,8,0' Text='20'/>
                <TextBlock x:Name='lblMaxSize' Grid.Column='4' Text='최대크기' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbMaxSize' Grid.Column='5' Margin='0,0,8,0' IsEditable='True'>
                  <ComboBoxItem Content='0 (제한없음)' Tag='0'/>
                  <ComboBoxItem Content='1920' Tag='1920'/>
                  <ComboBoxItem Content='1600' Tag='1600'/>
                  <ComboBoxItem Content='1280' Tag='1280'/>
                </ComboBox>
                <TextBlock Grid.Column='6' Text='FPS' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbMaxFps' Grid.Column='7' Margin='0,0,8,0' IsEditable='True'>
                  <ComboBoxItem Content='0 (자동)' Tag='0'/>
                  <ComboBoxItem Content='60' Tag='60'/>
                  <ComboBoxItem Content='120' Tag='120'/>
                </ComboBox>
                <TextBlock x:Name='lblRenderer' Grid.Column='8' Text='렌더러' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbRenderer' Grid.Column='9'>
                  <ComboBoxItem Content='자동 (D3D11)' Tag='auto'/>
                  <ComboBoxItem Content='OpenGL' Tag='opengl'/>
                </ComboBox>
              </Grid>
              <Grid Margin='0,0,0,9'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='110'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblVideoEncoder' Grid.Column='0' Text='인코더' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbVideoEncoder' Grid.Column='1' IsEditable='True' Margin='0,0,6,0'>
                  <ComboBoxItem Content='자동 (기본)' Tag=''/>
                </ComboBox>
                <Button x:Name='btnListEncoders' Grid.Column='2' Content='인코더 조회' Style='{StaticResource SubBtn}' Margin='0,0,10,0'/>
                <TextBlock x:Name='lblVideoBuffer' Grid.Column='3' Text='영상 버퍼' Style='{StaticResource FieldLabel}'/>
                <ComboBox x:Name='cmbVideoBuffer' Grid.Column='4' IsEditable='True'>
                  <ComboBoxItem Content='0 (자동)' Tag='0'/>
                  <ComboBoxItem Content='30 ms' Tag='30'/>
                  <ComboBoxItem Content='50 ms' Tag='50'/>
                  <ComboBoxItem Content='80 ms' Tag='80'/>
                  <ComboBoxItem Content='120 ms' Tag='120'/>
                </ComboBox>
              </Grid>
              <WrapPanel Margin='2,0,0,4'>
                <CheckBox x:Name='chkStayAwake' Content='절전 방지 (-w)'/>
                <CheckBox x:Name='chkLowLatency' Content='저지연 인코딩'/>
                <CheckBox x:Name='chkNoAudio' Content='소리 끄기'/>
                <CheckBox x:Name='chkPrintFps' Content='실시간 FPS 표시 (제목표시줄)'/>
              </WrapPanel>
            </StackPanel>
          </Border>

          <!-- Virtual Display & App Launch -->
          <Border Style='{StaticResource Card}'>
            <StackPanel>
              <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='2' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='txtSec3Title' Text='가상 디스플레이 · 앱 바로 실행' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
              <Grid Margin='0,0,0,8'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='170'/>
                  <ColumnDefinition Width='*'/>
                </Grid.ColumnDefinitions>
                <CheckBox x:Name='chkVirtualDisplay' Grid.Column='0' Content='가상 디스플레이 사용' VerticalAlignment='Center' Margin='0,0,10,0'/>
                <ComboBox x:Name='cmbVdSpec' Grid.Column='1' IsEditable='True' Margin='0,0,14,0'>
                  <ComboBoxItem Content='1920x1080/280'/>
                  <ComboBoxItem Content='2560x1440/373'/>
                  <ComboBoxItem Content='2560x1080/280'/>
                  <ComboBoxItem Content='3440x1440/373'/>
                </ComboBox>
                <CheckBox x:Name='chkNoVdDecorations' Grid.Column='2' Content='DeX / 시스템 UI 건너뛰기' VerticalAlignment='Center' Margin='0'/>
              </Grid>
              <Grid Margin='0,0,0,8'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='155'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblApp' Grid.Column='0' Text='실행 앱' Style='{StaticResource FieldLabel}'/>
                <Grid Grid.Column='1' Margin='0,0,6,0'>
                  <TextBox x:Name='txtAppSearch'/>
                  <TextBlock x:Name='txtAppSearchHint' Text='앱 검색...' FontSize='11.5' Foreground='#8F8677' IsHitTestVisible='False' VerticalAlignment='Center' Margin='9,0,0,0'/>
                </Grid>
                <ComboBox x:Name='cmbApps' Grid.Column='2' Margin='0,0,6,0'/>
                <Button x:Name='btnRefreshApps' Grid.Column='3' Content='앱 불러오기' Style='{StaticResource SubBtn}'/>
              </Grid>
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblExtraArgs' Grid.Column='0' Text='scrcpy 추가 옵션' Style='{StaticResource FieldLabel}'/>
                <Grid Grid.Column='1'>
                  <TextBox x:Name='txtExtraArgs'/>
                  <TextBlock x:Name='txtExtraArgsHint' Text='순정 scrcpy 옵션 직접 추가 (예: --window-borderless --shortcut-mod=lctrl)' FontSize='11' Foreground='#8F8677' IsHitTestVisible='False' VerticalAlignment='Center' Margin='9,0,0,0'/>
                </Grid>
              </Grid>
            </StackPanel>
          </Border>

          <!-- Wireless Pairing & Storage & Help -->
          <Border Style='{StaticResource Card}'>
            <StackPanel>
              <StackPanel Orientation='Horizontal' Margin='0,0,0,9'>
                <Border Style='{StaticResource StepBadge}'><TextBlock Text='3' Style='{StaticResource StepNum}'/></Border>
                <TextBlock x:Name='lblAdvSec3' Text='수동 무선 연결 · 저장 폴더 · 도움말' Style='{StaticResource SectionTitle}'/>
              </StackPanel>
              <Grid Margin='0,0,0,6'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='120'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='90'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblPairAddr' Grid.Column='0' Text='① 페어링 IP:포트' Style='{StaticResource FieldLabel}'/>
                <TextBox x:Name='txtPairAddr' Grid.Column='1' Margin='0,0,8,0'/>
                <TextBlock x:Name='lblPairCode' Grid.Column='2' Text='6자리 코드' Style='{StaticResource FieldLabel}'/>
                <TextBox x:Name='txtPairCode' Grid.Column='3' Margin='0,0,8,0'/>
                <Button x:Name='btnPair' Grid.Column='4' Content='페어링' Style='{StaticResource SubBtn}' Width='76'/>
              </Grid>
              <Grid Margin='0,0,0,10'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='120'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblWifiAddr' Grid.Column='0' Text='② 무선 IP:포트' Style='{StaticResource FieldLabel}'/>
                <TextBox x:Name='txtWifiAddr' Grid.Column='1' Margin='0,0,8,0'/>
                <Button x:Name='btnWifiConnect' Grid.Column='2' Content='무선 연결' Style='{StaticResource SoftGreenBtn}' Width='76'/>
              </Grid>
              <Grid Margin='0,0,0,10'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='lblRecFolder' Grid.Column='0' Text='캡처 · 녹화 폴더' Style='{StaticResource FieldLabel}'/>
                <TextBox x:Name='txtRecordFolder' Grid.Column='1' IsReadOnly='True' Background='#F5F1E8' Foreground='#5C5548' Margin='0,0,6,0'/>
                <Button x:Name='btnBrowseRecord' Grid.Column='2' Content='폴더 선택' Style='{StaticResource SubBtn}' Margin='0,0,5,0'/>
                <Button x:Name='btnOpenRecord' Grid.Column='3' Content='폴더 열기' Style='{StaticResource SubBtn}'/>
              </Grid>
              <WrapPanel>
                <Button x:Name='btnSelectFolder' Content='scrcpy 폴더 변경' Style='{StaticResource SubBtn}' Margin='0,0,6,0'/>
                <Button x:Name='btnSetupGuide' Content='연결 방법' Style='{StaticResource SubBtn}' Margin='0,0,6,0'/>
                <Button x:Name='btnShortcuts' Content='단축키 안내' Style='{StaticResource SubBtn}' Margin='0,0,6,0'/>
                <Button x:Name='btnCopyCmd' Content='실행 명령어 복사' Style='{StaticResource SubBtn}'/>
              </WrapPanel>
            </StackPanel>
          </Border>
        </StackPanel>
      </ScrollViewer>
    </Grid>

    <!-- DYNAMIC OPTION GUIDE BAR -->
    <Border Grid.Row='1' Background='#EFE9DC' BorderBrush='#DDD4C3' BorderThickness='1' CornerRadius='8' Padding='10,6' Margin='0,6,0,8'>
      <DockPanel>
        <Border Background='#2C5E3F' CornerRadius='4' Padding='6,1.5' Margin='0,0,8,0' VerticalAlignment='Center' DockPanel.Dock='Left'>
          <TextBlock x:Name='txtGuideTitle' Text='옵션 가이드' FontSize='10.5' FontWeight='Bold' Foreground='#FFFDF8'/>
        </Border>
        <TextBlock x:Name='txtGuideBody' Text='각 옵션 위에 마우스를 올리면 설명이 표시됩니다.' FontSize='11.5' Foreground='#3D372E' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
      </DockPanel>
    </Border>

    <!-- BOTTOM FIXED LAUNCH & ACTION BAR (Concept B) -->
    <Grid Grid.Row='2'>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width='*'/>
        <ColumnDefinition Width='Auto'/>
        <ColumnDefinition Width='Auto'/>
        <ColumnDefinition Width='Auto'/>
        <ColumnDefinition Width='Auto'/>
        <ColumnDefinition Width='Auto'/>
      </Grid.ColumnDefinitions>
      <Button x:Name='btnStart' Grid.Column='0' Content='▶  몬헌 아웃랜더스 실행' Style='{StaticResource PrimaryBtn}' Margin='0,0,6,0'/>
      <Button x:Name='btnStop' Grid.Column='1' Content='■ 중지' Style='{StaticResource DangerBtn}' Margin='0,0,6,0' IsEnabled='False'/>
      <Button x:Name='btnScreenshot' Grid.Column='2' Content='📷 스샷 (F12)' Style='{StaticResource ActionSubBtn}' Margin='0,0,5,0'/>
      <Button x:Name='btnRecordToggle' Grid.Column='3' Content='● 녹화' Style='{StaticResource ActionSubBtn}' Margin='0,0,5,0'/>
      <Button x:Name='btnBottomOpenFolder' Grid.Column='4' Content='📁 폴더' Style='{StaticResource ActionSubBtn}' Margin='0,0,5,0'/>
      <Button x:Name='btnToggleLog' Grid.Column='5' Content='📋 로그' Style='{StaticResource ActionSubBtn}'/>
    </Grid>

    <!-- COLLAPSIBLE LOG DRAWER (Hidden by default!) -->
    <Border x:Name='bdLogDrawer' Grid.Row='3' Background='#1B241E' CornerRadius='10' Padding='10,8' Margin='0,8,0,0' Height='130' Visibility='Collapsed'>
      <Grid>
        <Grid.RowDefinitions>
          <RowDefinition Height='Auto'/>
          <RowDefinition Height='*'/>
        </Grid.RowDefinitions>
        <Grid Grid.Row='0' Margin='2,0,2,5'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='*'/>
            <ColumnDefinition Width='Auto'/>
            <ColumnDefinition Width='Auto'/>
          </Grid.ColumnDefinitions>
          <TextBlock x:Name='lblLog' Grid.Column='0' Text='실시간 로그' FontSize='11' FontWeight='SemiBold' Foreground='#A7C4B0' VerticalAlignment='Center'/>
          <Border x:Name='bdFpsBadge' Grid.Column='1' Background='#26332B' BorderBrush='#3E5246' BorderThickness='1' CornerRadius='10' Padding='8,1.5' Margin='0,0,6,0' VerticalAlignment='Center' Visibility='Collapsed'>
            <TextBlock x:Name='txtFpsBadge' Text='FPS: --' FontSize='10.5' FontWeight='Bold' Foreground='#4ADE80'/>
          </Border>
          <Button x:Name='btnClearLog' Grid.Column='2' Content='지우기' Style='{StaticResource DarkGhostBtn}'/>
        </Grid>
        <TextBox x:Name='txtLog'
                 Grid.Row='1'
                 IsReadOnly='True'
                 Background='Transparent'
                 Foreground='#E6EFE9'
                 BorderThickness='0'
                 Height='Auto'
                 Padding='0'
                 FontFamily='Consolas, Malgun Gothic'
                 FontSize='11'
                 TextWrapping='Wrap'
                 VerticalContentAlignment='Top'
                 VerticalScrollBarVisibility='Auto'
                 AcceptsReturn='True'>
          <TextBox.Template>
            <ControlTemplate TargetType='TextBox'>
              <ScrollViewer x:Name='PART_ContentHost'/>
            </ControlTemplate>
          </TextBox.Template>
        </TextBox>
      </Grid>
    </Border>
  </Grid>
</Grid>";

            return (UIElement)XamlReader.Parse(xaml);
        }
    }
}
