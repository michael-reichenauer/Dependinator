class Icons {

    svgs = []

    constructor() {
        const svgFiles = require.context("../resources/icons", true, /\.(svg)$/)
        this.svgs = svgFiles
            .keys()
            .map(key => {
                const file = svgFiles(key)
                const src = file.default

                const name = src
                    .slice(33)               // Skip prefix e.g. '/static/media/10165-icon-service-'
                    .slice(0, -13)           // Skip sufic e.g. '.4e31000e.svg'
                    .replaceAll('-', ' ')

                return { key: key, name: name, src: src }
            })
        // console.log('svg', this.svgs)
    }

    getIconSrc(key) {
        const svg = this.svgs.find(svg => svg.key === key)
        if (svg === undefined) {
            return 'apple-touch-icon.png'
        }
        return svg.src
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