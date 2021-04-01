
import axios from 'axios';


export default class Api {
    async getMessage() {
        const rsp = await axios.get(`/api/message`)
        console.log('rsp', rsp)
        let { text } = rsp.data
        return text
    }
}