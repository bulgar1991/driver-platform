if (!Array.prototype.at) {
    Array.prototype.at = function(index) {

      index = Math.trunc(index) || 0;

      if (index < 0) index += this.length;

      if (index < 0 || index >= this.length) return undefined;
      return this[index];
    };
  }