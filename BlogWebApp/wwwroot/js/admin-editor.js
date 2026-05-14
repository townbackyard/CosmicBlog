window.cosmicblog = window.cosmicblog || {};
window.cosmicblog.adminEditor = {
    init: function(opts) {
        var textarea = document.querySelector(opts.textareaSelector);
        if (!textarea) return;
        // EasyMDE replaces the textarea with its own DOM. Configure later
        // (autosave + image upload + status UI) in Tasks 7, 8, 10, 11.
        var editor = new EasyMDE({
            element: textarea,
            spellChecker: false,
            status: ['lines', 'words'],
            renderingConfig: { singleLineBreaks: false },
        });
        this._editor = editor;
        this._opts = opts;
    },
};
