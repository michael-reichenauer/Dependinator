
import axios from 'axios';


export default class Api {
    async getMessage() {
        const rsp = await axios.get(`/api/message`)
        console.log('rsp', rsp)
        let { text } = rsp.data
        return text
    }

    async newDiagram(diagramId, name, canvasData) {
        try {
            await axios.post('/api/NewDiagram', {
                diagramId: diagramId,
                name: name,
                canvasData: canvasData
            });
        } catch (error) {
            console.error('failed to post')
        }

    }
}