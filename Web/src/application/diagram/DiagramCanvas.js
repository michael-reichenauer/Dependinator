import "import-jquery";
import "jquery-ui-bundle";
import "jquery-ui-bundle/jquery-ui.css";
import PubSub from 'pubsub-js'
import cuid from 'cuid'
import { imgDataUrlToPngDataUrl, publishAsDownload, random } from '../../common/utils'
import Node from './Node'
import { store } from "./Store";
import Canvas from "./Canvas";
import CanvasStack from "./CanvasStack";
import { zoomAndMoveShowTotalDiagram } from "./showTotalDiagram";
import { addDefaultNewDiagram, addFigureToCanvas } from "./addDefault";
import InnerDiagramCanvas from "./InnerDiagramCanvas";
import Printer from "../../common/Printer";
import { setProgress } from "../../common/Progress";
import { setErrorMessage } from "../../common/MessageSnackbar";
import NodeGroup from './NodeGroup';
import { greenNumberIconKey } from "../../common/icons";
import NodeNumber from "./NodeNumber";
import { svgToSvgDataUrl, fetchFiles } from './../../common/utils';


export default class DiagramCanvas {
    static defaultWidth = 100000
    static defaultHeight = 100000

    canvasStack = null
    store = store
    inner = null

    canvas = null;
    callbacks = null

    constructor(htmlElementId, callbacks) {
        this.callbacks = callbacks
        this.canvas = new Canvas(this, htmlElementId, this.onEditMode, DiagramCanvas.defaultWidth, DiagramCanvas.defaultHeight)
        this.canvasStack = new CanvasStack(this.canvas)
        this.inner = new InnerDiagramCanvas(this.canvas, this.canvasStack, this.store)
    }

    init() {
        this.loadInitialDiagram()

        this.handleDoubleClick(this.canvas)
        this.handleEditChanges(this.canvas)
        this.handleSelect(this.canvas)
        this.handleCommands()
    }

    delete() {
        this.canvas.destroy()
    }

    handleCommands = () => {
        PubSub.subscribe('canvas.Undo', () => this.commandUndo())
        PubSub.subscribe('canvas.Redo', () => this.commandRedo())

        PubSub.subscribe('canvas.AddNode', (_, data) => this.addNode(data))
        PubSub.subscribe('canvas.AddGroup', (_, data) => this.addGroup(data.position))

        PubSub.subscribe('canvas.ShowTotalDiagram', this.showTotalDiagram)

        PubSub.subscribe('canvas.EditInnerDiagram', this.commandEditInnerDiagram)
        PubSub.subscribe('canvas.TuneSelected', (_, data) => this.commandTuneSelected(data.x, data.y))
        PubSub.subscribe('canvas.PopInnerDiagram', this.commandPopFromInnerDiagram)

        PubSub.subscribe('canvas.SetEditMode', (_, isEditMode) => this.canvas.panPolicy.setEditMode(isEditMode))
        PubSub.subscribe('canvas.NewDiagram', this.commandNewDiagram)
        PubSub.subscribe('canvas.OpenDiagram', this.commandOpenDiagram)
        PubSub.subscribe('canvas.RenameDiagram', (_, name) => this.commandRenameDiagram(name))
        PubSub.subscribe('canvas.DeleteDiagram', this.commandDeleteDiagram)
        PubSub.subscribe('canvas.SaveDiagramToFile', this.commandSaveToFile)
        PubSub.subscribe('canvas.Save', () => this.save())
        PubSub.subscribe('canvas.OpenFile', this.commandOpenFile)
        PubSub.subscribe('canvas.ArchiveToFile', this.commandArchiveToFile)
        PubSub.subscribe('canvas.Print', this.commandPrint)
        PubSub.subscribe('canvas.Export', (_, data) => this.commandExport(data))
    }


    commandUndo = () => {
        this.canvas.getCommandStack().undo()
        this.save()
    }

    commandRedo = () => {
        this.canvas.getCommandStack().redo()
    }

    commandNewDiagram = async () => {
        setProgress(true)
        try {
            //store.loadFile(file => console.log('File:', file))
            this.canvas.diagramName = 'Name'
            this.canvas.clearDiagram()
            await this.createNewDiagram()
            this.callbacks.setTitle(this.getTitle())
            this.showTotalDiagram()
        } catch (error) {
            setErrorMessage('Failed to create new diagram')
        }
        finally {
            setProgress(false)
        }
    }

    commandOpenDiagram = async (msg, diagramId) => {
        setProgress(true)
        try {
            console.log('open', diagramId)
            const canvasData = await this.store.openDiagramRootCanvas(diagramId)

            this.canvas.clearDiagram()

            // Deserialize canvas
            this.canvas.deserialize(canvasData)

            this.callbacks.setTitle(this.getTitle())
            this.showTotalDiagram()
        } catch (error) {
            setErrorMessage('Failed to load diagram')
        }
        finally {
            setProgress(false)
        }
    }

    commandRenameDiagram = async (name) => {
        this.setName(name)
        this.save()
    }

    commandDeleteDiagram = async () => {
        setProgress(true)
        try {
            await this.store.deleteDiagram(this.canvas.diagramId)
            this.canvas.clearDiagram()

            // Try get first diagram to open
            const canvasData = await this.store.openMostResentDiagramCanvas()

            // Deserialize canvas
            this.canvas.deserialize(canvasData)
            this.callbacks.setTitle(this.getTitle())
            this.showTotalDiagram()
        } catch (error) {
            // Failed to open most resent diagram, lets create new diagram
            await this.createNewDiagram()
            this.callbacks.setTitle(this.getTitle())
            this.showTotalDiagram()
        } finally {
            setProgress(false)
        }
    }

    commandSaveToFile = () => {
        this.store.saveDiagramToFile(this.canvas.diagramId)
    }

    commandOpenFile = async () => {
        setProgress(true)
        try {
            const diagramId = await this.store.loadDiagramFromFile()
            this.commandOpenDiagram('', diagramId)
        } catch (error) {
            setErrorMessage('Failed to load file')
        } finally {
            setProgress(false)
        }

    }

    commandArchiveToFile = async () => {
        setProgress(true)
        try {
            this.store.saveAllDiagramsToFile()
        } catch (error) {
            setErrorMessage('Failed to save all diagram')
        } finally {
            setProgress(false)
        }

    }

    commandPrint = () => {
        this.withWorkingIndicator(() => {
            const diagram = this.store.getDiagram(this.canvas.diagramId)

            const pages = diagram.canvases.map(d => this.canvas.exportAsSvg(d))
            const printer = new Printer()
            printer.print(pages)
        })
    }

    commandExport = (data) => {
        this.withWorkingIndicator(() => {
            const diagram = this.store.getDiagram(this.canvas.diagramId)
            const diagramName = diagram.diagramInfo.name

            let pages = diagram.canvases.map(d => this.canvas.exportAsSvg(d))
            let svgText = pages[0]

            const nestedSvgPaths = this.parseNestedSvgPaths(svgText)

            fetchFiles(nestedSvgPaths, files => {
                console.log('files', files)
                svgText = this.replacePathsWithSvgDataUrls(svgText, nestedSvgPaths, files)

                let svgDataUrl = svgToSvgDataUrl(svgText)

                if (data.type === 'png') {
                    const width = 793.7007874 // "210mm"
                    const height = 1046.9291339 // "277mm" 

                    imgDataUrlToPngDataUrl(svgDataUrl, width, height, pngDataUrl => {
                        publishAsDownload(pngDataUrl, `${diagramName}.png`);
                    })
                } else if (data.type === 'svg') {
                    publishAsDownload(svgDataUrl, `${diagramName}.svg`);
                }
            })


            if (data.type === 'svg') {
                // pages = diagram.canvases.map(d => this.canvas.exportAsSvg(d))
            } else if (data.type === 'png') {
                // pages = diagram.canvases.map(d => this.canvas.exportAsSvg(d))

                // var svgElement = document.getElementById('canvas').firstElementChild
                // let { width, height } = svgElement.getBBox();
                // //  console.log('svg', svgElement, width, height)
                // let clonedSvgElement = svgElement.cloneNode(true);
                // let outerHTML = clonedSvgElement.outerHTML
                // //console.log('html', outerHTML)

                // let iconImage = new Image();
                // iconImage.onload = () => {
                //     console.log('iconImage', iconImage)
                //     let canvas = document.createElement('canvas');
                //     canvas.width = 30;
                //     canvas.height = 30;
                //     let context = canvas.getContext('2d');
                //     // draw image in canvas starting left-0 , top - 0  
                //     context.drawImage(iconImage, 0, 0, width, height);
                //     //  downloadImage(canvas); need to implement

                //     // let png = canvas.toDataURL(); // default png
                //     console.log('icon:', canvas.innerHTML)

                // }
                // iconImage.src = '/static/media/10035-icon-service-App-Services.bdfe9ddd.svg'


                // fetch('/static/media/10035-icon-service-App-Services.bdfe9ddd.svg').then(f => {
                //     f.text().then(t => {
                //         const fileData = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(t)

                //         console.log('parse')
                //         const paths = this.parsePaths(outerHTML)
                //         console.log('paths', paths)
                //         this.getFiles(paths, icons => {
                //             console.log('files', icons)
                //             for (let i = 0; i < paths.length; i++) {
                //                 const path = paths[i];
                //                 const icon = icons[i]
                //                 const iconUrl = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(icon)
                //                 console.log('path', path)
                //                 console.log('url', iconUrl)

                //                 outerHTML = outerHTML.replaceAll(`xlink:href="${path}"`, `xlink:href="${iconUrl}"`)
                //             }

                //             outerHTML = outerHTML.replace('<svg height="100000" version="1.1" width="100000" ',
                //                 '<svg width="210mm" height="277mm" version="1.1" viewBox="48499.25921624277 48791.66970379388 863.7007874 1192.519685" ')

                //             let blob = new Blob([outerHTML], { type: 'image/svg+xml;charset=utf-8' });
                //             let URL = window.URL || window.webkitURL || window;
                //             let blobURL = URL.createObjectURL(blob);
                //             let image = new Image();
                //             image.onload = () => {
                //                 let canvas = document.createElement('canvas');
                //                 canvas.width = 793.7007874;
                //                 canvas.height = 1046.9291339;
                //                 let context = canvas.getContext('2d');
                //                 // draw image in canvas starting left-0 , top - 0  
                //                 context.drawImage(image, 0, 0, canvas.width, canvas.height);
                //                 //  downloadImage(canvas); need to implement

                //                 let png = canvas.toDataURL(); // default png
                //                 //console.log('png', png)

                //                 var newWindow = window.open("");
                //                 newWindow.document.open();
                //                 newWindow.document.write(`<html><body>${outerHTML}</body></html>`);
                //                 newWindow.document.close();

                //                 var download = function (href, name) {
                //                     var link = document.createElement('a');
                //                     link.download = name;
                //                     link.style.opacity = "0";
                //                     document.body.append(link);
                //                     link.href = href;
                //                     link.click();
                //                     link.remove();
                //                 }
                //                 download(png, "image.png");
                //             };

                //             image.src = blobURL;

                //         })

                //         // outerHTML = outerHTML.replace('xlink:href="/static/media/10035-icon-service-App-Services.bdfe9ddd.svg"',
                //         //     `xlink:href="${fileData}"`)
                //         // outerHTML = outerHTML.replace('xlink:href="/static/media/10162-icon-service-Cognitive-Services.d5e477dc.svg"',
                //         //     `xlink:href="${fileData}"`)
                //         //  outerHTML = outerHTML.replace('<svg height="100000" version="1.1" width="100000" ',
                //         //     '<svg width="210mm" height="277mm" version="1.1" viewBox="48499.25921624277 48791.66970379388 863.7007874 1192.519685" ')
                //         // //console.log('html', outerHTML)

                //         let blob = new Blob([outerHTML], { type: 'image/svg+xml;charset=utf-8' });
                //         let URL = window.URL || window.webkitURL || window;
                //         let blobURL = URL.createObjectURL(blob);
                //         let image = new Image();
                //         image.onload = () => {
                //             let canvas = document.createElement('canvas');
                //             canvas.widht = width;
                //             canvas.height = height;
                //             let context = canvas.getContext('2d');
                //             // draw image in canvas starting left-0 , top - 0  
                //             context.drawImage(image, 0, 0, width, height);
                //             //  downloadImage(canvas); need to implement

                //             let png = canvas.toDataURL(); // default png
                //             //console.log('png', png)

                //             // var newWindow = window.open("");
                //             // newWindow.document.open();
                //             // newWindow.document.write(`<html><body>${outerHTML}</body></html>`);
                //             // newWindow.document.close();

                //             // var download = function (href, name) {
                //             //     var link = document.createElement('a');
                //             //     link.download = name;
                //             //     link.style.opacity = "0";
                //             //     document.body.append(link);
                //             //     link.href = href;
                //             //     link.click();
                //             //     link.remove();
                //             // }
                //             // download(png, "image.png");

                //         };
                //         image.src = blobURL;

                //     })
                // })



                // pages = diagram.canvases.map(d => this.canvas.exportAsSvg(d))
                // console.log('png')
                // let canvas = document.createElement('canvas');
                // let ctx = canvas.getContext('2d')
                // //var canvas = document.getElementById("canvas");
                // var img1 = new Image();
                // img1.onload = function () {
                //     ctx.drawImage(img1, 30, 30);
                // }
                // img1.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(pages[0])

                // var img = canvas.toDataURL("image/png");
                // //   img = `data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==`
                // pages[0] = `<html><body><img src="${img}" height="500" width="500"/></body></html>`

                // console.log('pages', pages)
                // pages = diagram.canvases.map(d => this.canvas.exportAsPng(d))
            }
        })
    }

    parseNestedSvgPaths(text) {
        const regexp = new RegExp('xlink:href="/static/media[^"]*', 'g');

        let uniquePaths = []

        let match;
        while ((match = regexp.exec(text)) !== null) {
            const ref = `${match[0]}`
            const path = ref.substring(12)
            if (!uniquePaths.includes(path)) {
                uniquePaths.push(path)
            }
        }
        return uniquePaths
    }

    replacePathsWithSvgDataUrls(svgText, paths, svgImages) {
        for (let i = 0; i < paths.length; i++) {
            const path = paths[i];
            const svgImage = svgImages[i]
            const svgDataUrl = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svgImage)
            svgText = svgText.replaceAll(`xlink:href="${path}"`, `xlink:href="${svgDataUrl}"`)
        }
        return svgText
    }

    commandEditInnerDiagram = (msg, figure) => {
        this.withWorkingIndicator(() => {
            this.inner.editInnerDiagram(figure)
            this.callbacks.setTitle(this.getTitle())
            this.updateToolbarButtonsStates()
            this.save()
        });
    }

    commandTuneSelected = (x, y) => {
        console.log('tune')
        // Get target figure or use canvas as target
        let target = this.canvas.getSelection().primary

        if (typeof target?.getContextMenuItems !== "function") {
            // No context menu on target
            return
        }

        const menuItems = target.getContextMenuItems()
        this.callbacks.setContextMenu({ items: menuItems, x: x, y: y });
    }

    commandPopFromInnerDiagram = () => {
        this.withWorkingIndicator(() => {
            this.inner.popFromInnerDiagram()
            this.callbacks.setTitle(this.getTitle())
            this.updateToolbarButtonsStates()
            this.save()
        });
    }




    onEditMode = (isEditMode) => {
        this.callbacks.setEditMode(isEditMode)
        if (!isEditMode) {
            this.callbacks.setSelectMode(false)
        }

        if (!isEditMode) {
            // Remove grid
            this.canvas.setNormalBackground()
            return
        }

        this.canvas.setGridBackground()
    }

    showTotalDiagram = () => zoomAndMoveShowTotalDiagram(this.canvas)

    addNode = (data) => {
        if (data.group) {
            this.addGroup(data.icon, data.position)
            return
        }

        if (data.icon === greenNumberIconKey) {
            this.addNumber(data)
            return
        }
        var { icon, position } = data
        if (!position) {
            position = this.getCenter()
        }

        var options = null
        if (icon) {
            options = { icon: icon }
        }

        const node = new Node(Node.nodeType, options)
        const x = position.x - node.width / 2
        const y = position.y - node.height / 2

        addFigureToCanvas(this.canvas, node, x, y)
    }

    addGroup = (icon, position) => {
        const group = new NodeGroup({ icon: icon })
        var x = 0
        var y = 0

        if (!position) {
            position = this.getCenter()
            x = position.x - group.width / 2
            y = position.y - 20
        } else {
            x = position.x - 20
            y = position.y - 20
        }

        addFigureToCanvas(this.canvas, group, x, y)
    }

    addNumber = (data) => {
        var { position } = data
        if (!position) {
            position = this.getCenter()
        }

        const node = new NodeNumber()
        const x = position.x - node.width / 2
        const y = position.y - node.height / 2

        addFigureToCanvas(this.canvas, node, x, y)
    }

    tryGetFigure = (x, y) => {
        let cp = this.canvas.fromDocumentToCanvasCoordinate(x, y)
        let figure = this.canvas.getBestFigure(cp.x, cp.y)
        return figure
    }

    save() {
        // Serialize canvas figures and connections into canvas data object
        const canvasData = this.canvas.serialize();
        this.store.setCanvas(canvasData)
    }

    async loadInitialDiagram() {
        setProgress(true)
        try {
            try {
                await store.initialize()
            } catch (error) {
                setErrorMessage('Failed to connect to cloud server')
            }

            // Get the last used diagram and show 
            const canvasData = await this.store.openMostResentDiagramCanvas()
            this.canvas.deserialize(canvasData)
            this.callbacks.setTitle(this.getTitle())
            this.showTotalDiagram()
        } catch (error) {
            // No resent diagram data, lets create new diagram
            await this.createNewDiagram()
        }
        finally {
            setProgress(false)
        }
    }

    async activated() {
        try {
            if (!await this.store.serverHadChanges()) {
                return
            }

            const diagramId = this.store.getMostResentDiagramId()
            if (!diagramId) {
                throw new Error('No resent diagram')
            }

            this.commandOpenDiagram('', diagramId)
        } catch (error) {
            // No resent diagram data, lets create new diagram
            setErrorMessage('Activation error')
        }
    }

    createNewDiagram = async () => {
        const diagramId = cuid()
        this.canvas.diagramId = diagramId
        addDefaultNewDiagram(this.canvas)

        const canvasData = this.canvas.serialize();
        await this.store.newDiagram(diagramId, this.getName(), canvasData)
    }

    getCenter() {
        let x = (this.canvas.getWidth() / 2 + random(-10, 10) + this.canvas.getScrollLeft()) * this.canvas.getZoom()
        let y = (100 + random(-10, 10) + this.canvas.getScrollTop()) * this.canvas.getZoom()

        return { x: x, y: y }
    }



    handleEditChanges(canvas) {
        this.updateToolbarButtonsStates()

        canvas.commandStack.addEventListener(e => {
            // console.log('change event:', e)
            this.updateToolbarButtonsStates()


            if (e.isPostChangeEvent()) {
                // console.log('event isPostChangeEvent:', e)
                // if (e.command?.figure?.parent?.id === this.canvas.mainNodeId) {
                //     // Update the title whenever the main node changes
                //     this.callbacks.setTitle(this.getTitle())
                //     this.store.setDiagramName(this.canvas.diagramId, this.getName())
                // }

                if (e.action === "POST_EXECUTE") {
                    // console.log('save')
                    this.save()
                }
            }
        });
    }

    getTitle() {
        const name = this.getName()
        switch (this.canvasStack.getLevel()) {
            case 0:
                return name + ''
            case 1:
                return name + ' - Container'
            case 2:
                return name + ' - Component'
            default:
                return name + ' - Code'
        }
    }

    getName() {
        return this.canvas.diagramName ?? 'Name'
    }

    setName(name) {
        this.canvas.diagramName = name
        this.callbacks.setTitle(this.getTitle())
    }

    updateToolbarButtonsStates() {
        this.callbacks.setCanPopDiagram(!this.canvasStack.isRoot())
        this.callbacks.setCanUndo(this.canvas.getCommandStack().canUndo())
        this.callbacks.setCanRedo(this.canvas.getCommandStack().canRedo())
    }

    handleDoubleClick(canvas) {
        canvas.on('dblclick', (emitter, event) => {
            if (event.figure !== null) {
                return
            }

            if (!this.canvasStack.isRoot()) {
                // double click out side group node in inner diagram lets pop
                this.commandPopFromInnerDiagram()
                return
            }
            PubSub.publish('nodes.showDialog', { add: true, x: event.x, y: event.y })
        });
    }


    handleSelect(canvas) {
        canvas.on('select', (emitter, event) => {
            if (event.figure !== null) {
                this.callbacks.setSelectMode(true)
            }
            else {
                this.callbacks.setSelectMode(false)
            }
        });
    }

    withWorkingIndicator(action) {
        setProgress(true)
        setTimeout(() => {
            action()
            setProgress(false)
        }, 20);
    }
}
