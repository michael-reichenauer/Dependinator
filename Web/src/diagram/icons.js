
import draw2d from "draw2d";

const nodeIcons = {
    Node: () => new draw2d.shape.icon.Ipad(),
    User: () => new draw2d.shape.icon.User(),
    External: () => new draw2d.shape.icon.NewWindow(),

    IPhone: () => new draw2d.shape.icon.Iphone(),
    DB: () => new draw2d.shape.icon.Db(),
    Cloud: () => new draw2d.shape.icon.Cloud(),
    Gear: () => new draw2d.shape.icon.Gear(),
    Key: () => new draw2d.shape.icon.Key(),

    Ie: () => new draw2d.shape.icon.Ie(),
    Chrome: () => new draw2d.shape.icon.Chrome(),
    Safari: () => new draw2d.shape.icon.Safari(),
    Firefox: () => new draw2d.shape.icon.Firefox(),

    Windows: () => new draw2d.shape.icon.Windows(),
    Linux: () => new draw2d.shape.icon.Linux(),
    Apple: () => new draw2d.shape.icon.Apple(),

    Europe: () => new draw2d.shape.icon.GlobeAlt(),
    Americas: () => new draw2d.shape.icon.Globe(),
    Asia: () => new draw2d.shape.icon.GlobeAlt(),
}

export const getNodeIconNames = () => Object.entries(nodeIcons).map(e => e[0])

export const createNodeIcon = (name) => {
    if (name == null) {
        return null
    }

    const create = nodeIcons[name]
    if (create == null) {
        return null

    }

    return create()
}