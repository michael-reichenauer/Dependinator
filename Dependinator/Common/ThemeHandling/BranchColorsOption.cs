using System.Collections.Generic;


namespace Dependinator.Common.ThemeHandling
{
    public class BranchColorsOption
    {
        public string comment =>
            "Branch colors. First color is master branch, second multi branch and rest normal branch colors.";

        public List<string> Colors { get; set; } = new List<string>
        {
            "#FFE540FF", // master branch (violet)
            "#FFFFB300", // Vivid Yellow
            "#FFA12B8E", // Strong Purple
            "#FFFF6800", // Vivid Orange
            "#FF6892C0", // Very Light Blue
            "#FFDF334E", // Vivid Red
            "#FFCEA262", // Grayish Yellow
            "#FFAD7E62", // Medium Gray

            // The following will not be good for people with defective color vision
            "#FF0FA94E", // Vivid Green
            "#FFF6768E", // Strong Purplish Pink
            "#FF085E95", // Strong Blue
            "#FFFF7A5C", // Strong Yellowish Pink
            "#FF6D568D", // Strong Violet
            "#FFFF8E00", // Vivid Orange Yellow
            "#FFB04B6A", // Strong Purplish Red
            "#FFF4C800", // Vivid Greenish Yellow
            "#FFA5574F", // Strong Reddish Brown
            "#FF93AA00", // Vivid Yellowish Green
            "#FF9C5E2C", // Deep Yellowish Brown
            "#FFF13A13", // Vivid Reddish Orange
            "#FF86A854" // Dark Olive Green
        };
    }
}
