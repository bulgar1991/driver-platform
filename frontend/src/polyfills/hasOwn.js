if(!Object.hasOwn) {
    Object.hasOwn = function(target, key) {
        return Object.prototype.hasOwnProperty.call(target, key);
    };
}