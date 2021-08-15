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

                if (path.startsWith('./DefaultIcon')) {
                    name = 'Default Icon'
                    fullName = name
                    key = defaultKey
                }
                else if (path.startsWith('./Azure')) {
                    // Azure icons have a pattern like ./Azure/Storage/00776-icon-service-Azure-HCP-Cache.svg
                    const parts = path.split('/')
                    name = parts[3]
                        .slice(19)              // Skip prefix e.g. '10165-icon-service-'
                        .slice(0, -4)           // Skip sufic e.g. '.svg'
                        .replaceAll('-', ' ')
                    fullName = `${parts[1]}/${parts[2]}/${name}`
                    key = `${parts[1]}/${parts[2]}/${name}`.replaceAll(' ', '_')
                }

                // console.log('path', path, src)
                return { key: key, name: name, fullName: fullName, src: src }
            })
        console.log('svg', this.svgIcons.slice(0, 20))
    }

    getAllIcons() {
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