if(!window.structuredClone) {
    window.structuredClone = (obj) => {
        return JSON.parse(JSON.stringify(obj))
    }
}