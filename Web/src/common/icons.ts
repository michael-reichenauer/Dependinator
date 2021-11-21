// @ts-ignore
const svgFiles = require.context("../resources/icons", true, /\.(svg)$/);
export const defaultIconKey = "defaultIcon";
export const greenNumberIconKey = "greenNumberIcon";
export const noImageIconKey = "none";

interface IconType {
  key: string;
  name: string;
  fullName: string;
  src: any;
  root: string;
  group: string;
}

class Icons {
  svgIcons: IconType[] = [];

  constructor() {
    // Parse all svg files into an array of objects
    this.svgIcons = svgFiles.keys().map((path: string) => {
      const file = svgFiles(path);
      const src = file.default;

      const key = path.replaceAll(" ", "_");
      const name = path.replaceAll("-", " ");
      const fullName = name;
      let svg: IconType = {
        key: key,
        name: name,
        fullName: fullName,
        src: src,
        root: "",
        group: "",
      };

      if (path.startsWith("./DefaultIcon")) {
        svg = this._getDefaultSvg(svg);
      }
      if (path.startsWith("./green_number-1")) {
        svg = this._getGreenNumberSvg(svg);
      }
      if (path.startsWith("./no_image")) {
        svg = this._getNoImageSvg(svg);
      } else if (path.startsWith("./Azure")) {
        svg = this._getAzureSvg(svg, path);
      } else if (path.startsWith("./Aws")) {
        svg = this._getAwsSvg(svg, path);
      } else if (path.startsWith("./OSA")) {
        svg = this._getOsaSvg(svg, path);
      }

      //console.log('svg', svg)
      return svg;
    });
    //console.log('svg', this.svgIcons.slice(0, 20))
  }

  _getDefaultSvg(svg: IconType): IconType {
    return {
      ...svg,
      key: defaultIconKey,
      name: "Default Icon",
      fullName: "Default Icon",
    };
  }

  _getGreenNumberSvg(svg: IconType): IconType {
    return {
      ...svg,
      key: greenNumberIconKey,
      name: "Number Label",
      fullName: "Number Label",
    };
  }
  _getNoImageSvg(svg: IconType): IconType {
    return {
      ...svg,
      key: noImageIconKey,
      name: "No icon",
      fullName: "No icon",
    };
  }

  _getAzureSvg(svg: IconType, path: string): IconType {
    if (path.startsWith("./Azure/root-icon.svg")) {
      return { ...svg, key: "Azure", name: "Azure", fullName: "Azure" };
    }

    // Azure icons have a pattern like ./Azure/Storage/00776-icon-service-Azure-HCP-Cache.svg
    const parts = path.split("/").slice(1);
    const name = parts[2]
      .slice(19) // Skip prefix e.g. '10165-icon-service-'
      .slice(0, -4) // Skip sufic e.g. '.svg'
      .replaceAll("-", " ")
      .replaceAll("_", " ");
    const root = "Azure";
    const group = parts[1];
    const fullName = `${root}/${group}/${name}`;
    const key = `${root}/${group}/${name}`.replaceAll(" ", "");

    return {
      ...svg,
      key: key,
      root: root,
      group: group,
      name: name,
      fullName: fullName,
    };
  }

  _getAwsSvg(svg: IconType, path: string): IconType {
    if (path.startsWith("./Aws/root-icon.svg")) {
      return { ...svg, key: "Aws", name: "Aws", fullName: "Aws" };
    } else if (path.startsWith("./Aws/Architecture")) {
      return this._getAwsSubSvg(svg, path, "Architecture");
    } else if (path.startsWith("./Aws/Category")) {
      return this._getAwsSubSvg(svg, path, "Category");
    } else if (path.startsWith("./Aws/Resource")) {
      return this._getAwsSubSvg(svg, path, "Resource");
    }

    return this._getDefaultSvg(svg);
  }

  _getAwsSubSvg(svg: IconType, path: string, subType: string): IconType {
    // Aws icons have a pattern like ./Aws/Aws/Architecture-Service-Icons_07302021/Arch_Analytics/Arch_16
    const awsPath = path
      .replaceAll("/Architecture-Service-Icons_07302021", "")
      .replaceAll("/Category-Icons_07302021", "")
      .replaceAll("/Resource-Icons_07302021", "")
      .replaceAll("_16.svg", "")
      .replaceAll("/Arch_16", "")
      .replaceAll("/16", "")
      .replaceAll("Arch_Amazon-", "")
      .replaceAll("Arch_AWS-", "")
      .replaceAll("Arch_", "")
      .replaceAll("Arch-Category_16", "Category")
      .replaceAll("Arch-Category_", "")
      .replaceAll("/Arch-Category_", "/")
      .replaceAll("/Res_48_Dark", "")
      .replaceAll("/Res_48_Light", "")
      .replaceAll("/Res_", "/")
      .replaceAll("_48_Dark.svg", "")
      .replaceAll("_48_Light.svg", "")
      .replaceAll("_light-bg.svg", "")
      .replaceAll("-", " ")
      .replaceAll("_", " ");

    const parts = awsPath.split("/").slice(1);
    const root = "Aws";
    const name = parts[2];
    const group = `${subType}/${parts[1]}`;
    const fullName = `${root}/${group}/${name}`;
    const key = `${root}/${group.replaceAll("/", "")}/${name}`.replaceAll(
      " ",
      ""
    );

    return {
      ...svg,
      key: key,
      root: root,
      group: group,
      name: name,
      fullName: fullName,
    };
  }

  _getOsaSvg(svg: IconType, path: string): IconType {
    if (path.startsWith("./OSA/root-icon.svg")) {
      return { ...svg, key: "OSA", name: "OSA", fullName: "OSA" };
    }

    // Azure icons have a pattern like ./OSA/osa_cloud.svg
    const parts = path.split("/").slice(1);
    const name = parts[1]
      .slice(4) // Skip prefix e.g. 'osa_'
      .slice(0, -4) // Skip sufic e.g. '.svg'
      .replaceAll("-", " ")
      .replaceAll("_", " ");
    const root = "OSA";
    const group = "General";
    const fullName = `${root}/${group}/${name}`;
    const key = `${root}/${group}/${name}`.replaceAll(" ", "");

    return {
      ...svg,
      key: key,
      root: root,
      group: group,
      name: name,
      fullName: fullName,
    };
  }

  getAllIcons(): IconType[] {
    //console.log('icons length ', this.svgIcons.length)
    return [...this.svgIcons];
  }

  getIcon(key: string): IconType {
    const svg = this.svgIcons.find((svg) => svg.key === key);
    if (svg === undefined) {
      // Return default icon
      return this.svgIcons.find(
        (svg) => svg.key === defaultIconKey
      ) as IconType;
    }
    return svg;
  }

  //
  distance(s1: string, s2: string) {
    s1 = s1.toLowerCase();
    s2 = s2.toLowerCase();

    var costs = [];
    for (var i = 0; i <= s1.length; i++) {
      var lastValue = i;
      for (var j = 0; j <= s2.length; j++) {
        if (i === 0) costs[j] = j;
        else {
          if (j > 0) {
            var newValue = costs[j - 1];
            if (s1.charAt(i - 1) !== s2.charAt(j - 1))
              newValue = Math.min(Math.min(newValue, lastValue), costs[j]) + 1;
            costs[j - 1] = lastValue;
            lastValue = newValue;
          }
        }
      }
      if (i > 0) costs[s2.length] = lastValue;
    }
    return costs[s2.length];
  }
}

export const icons = new Icons();
