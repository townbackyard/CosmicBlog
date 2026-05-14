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
        // Tags: parse comma-separated input, enforce 12-cap, mirror to hidden field.
        var tagsInput = document.getElementById('tags-input');
        var tagsHidden = document.getElementById('tags-hidden');
        var tagsCount = document.getElementById('tags-count');
        if (tagsInput && tagsHidden && tagsCount) {
            var syncTags = function() {
                var raw = tagsInput.value || '';
                var tags = raw.split(',').map(function(t) { return t.trim(); }).filter(Boolean);
                if (tags.length > 12) {
                    tags = tags.slice(0, 12);
                    tagsInput.value = tags.join(', ');
                }
                tagsCount.textContent = tags.length + ' / 12 tags';
                tagsHidden.value = tags.join(',');
            };
            tagsInput.addEventListener('input', syncTags);
            syncTags();  // initial render
        }
        this._editor = editor;
        this._opts = opts;
    },
};
