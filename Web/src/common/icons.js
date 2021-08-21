const svgFiles = require.context("../resources/icons", true, /\.(svg)$/)
const defaultKey = 'defaultIcon'

class Icons {
    svgIcons = []

    constructor() {

        // Parse all svg files into an array of objects
        this.svgIcons = svgFiles
            .keys()
            .map(path => {
                const file = svgFiles(path)
                const src = file.default

                var name = path.replaceAll('-', ' ')
                var fullName = name
                var key = path.replaceAll(' ', '_')
                var parts = path.split('/').slice(1)
                var group = ''
                var root = ''

                if (path.startsWith('./DefaultIcon')) {
                    key = defaultKey
                    name = 'Default Icon'
                    fullName = name
                }
                else if (path.startsWith('./azure-official.svg')) {
                    key = 'Azure'
                    name = 'Azure'
                    fullName = name
                }
                else if (path.startsWith('./aws-official.svg')) {
                    key = 'Aws'
                    name = 'Aws'
                    fullName = name
                }
                else if (path.startsWith('./Azure')) {
                    // Azure icons have a pattern like ./Azure/Storage/00776-icon-service-Azure-HCP-Cache.svg
                    name = parts[2]
                        .slice(19)              // Skip prefix e.g. '10165-icon-service-'
                        .slice(0, -4)           // Skip sufic e.g. '.svg'
                        .replaceAll('-', ' ')
                    root = 'Azure'
                    group = parts[1]
                    fullName = `${root}/${group}/${name}`
                    key = `${root}/${group}/${name}`.replaceAll(' ', '_')
                } else if (path.startsWith('./Aws')) {
                    // Aws icons have a pattern like ./Aws/Aws/Architecture-Service-Icons_07302021/Arch_Analytics/Arch_16
                    const awsPath = path.replaceAll('/Architecture-Service-Icons_07302021', '')
                        .replaceAll('/Category-Icons_07302021', '').replaceAll('/Resource-Icons_07302021', '')
                        .replaceAll('_16.svg', '').replaceAll('/Arch_16', '').replaceAll('/16', '')
                        .replaceAll('Arch_Amazon-', '').replaceAll('Arch_AWS-', '').replaceAll('Arch_', '')
                        .replaceAll('Arch-Category_16', 'Category').replaceAll('Arch-Category_', '')
                        .replaceAll('/Arch-Category_', '/')
                        .replaceAll('/Res_48_Dark', '').replaceAll('/Res_48_Light', '').replaceAll('/Res_', '/')
                        .replaceAll('_48_Dark.svg', '').replaceAll('_48_Light.svg', '')
                        .replaceAll('-', ' ').replaceAll('_', ' ')

                    parts = awsPath.split('/').slice(1)
                    root = 'Aws'
                    name = parts[2]
                    group = parts[1]
                    fullName = `${root}/${group}/${name}`
                    key = `${root}/${group}/${name}`.replaceAll(' ', '_')
                }

                // console.log('path', path, src)
                const svg = { key: key, name: name, fullName: fullName, src: src, root: root, group: group }
                //console.log('svg', svg)
                return svg
            })
        //console.log('svg', this.svgIcons.slice(0, 20))
    }

    getAllIcons() {
        console.log('icons length ', this.svgIcons.length)
        return [...this.svgIcons]
    }

    getIcon(key) {
        const svg = this.svgIcons.find(svg => svg.key === key)
        if (svg === undefined) {
            // Return default icon
            return this.svgIcons.find(svg => svg.key === defaultKey)
        }
        return svg
    }

    // 
    distance(s1, s2) {
        s1 = s1.toLowerCase();
        s2 = s2.toLowerCase();

        var costs = [];
        for (var i = 0; i <= s1.length; i++) {
            var lastValue = i;
            for (var j = 0; j <= s2.length; j++) {
                if (i === 0)
                    costs[j] = j;
                else {
                    if (j > 0) {
                        var newValue = costs[j - 1];
                        if (s1.charAt(i - 1) !== s2.charAt(j - 1))
                            newValue = Math.min(Math.min(newValue, lastValue),
                                costs[j]) + 1;
                        costs[j - 1] = lastValue;
                        lastValue = newValue;
                    }
                }
            }
            if (i > 0)
                costs[s2.length] = lastValue;
        }
        return costs[s2.length];
    }
}

export const icons = new Icons()