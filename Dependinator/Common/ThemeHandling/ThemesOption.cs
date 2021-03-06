namespace Dependinator.Common.ThemeHandling
{
    public class ThemesOption
    {
        public string CurrentTheme = "Dark";
        public string comment0 => "Theme options. You can edit and add custom themes in the list.";
        public string comment1 => "Specify CurrentTheme name.";
        public string comment2 => "Default theme is read-only.";

        public ThemeOption[] CustomThemes { get; set; } =
        {
            new ThemeOption {Name = "Dark"},
            new ThemeOption {Name = "Light"}
        };

        public ThemeOption DefaultTheme => new ThemeOption {Name = "Default Dark (read-only)"};
    }
}
