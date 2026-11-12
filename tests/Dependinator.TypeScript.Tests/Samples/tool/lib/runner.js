function format(text) {
    return text.trim();
}

class Runner {
    start(text) {
        return format(text);
    }
}

exports.run = function (text, runner) {
    return runner.start(text);
};

module.exports.Runner = Runner;
module.exports.limit = 10;
