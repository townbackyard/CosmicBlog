window.cosmicblog = window.cosmicblog || {};
window.cosmicblog.adminEditor = {
    init: function(opts) {
        var textarea = document.querySelector(opts.textareaSelector);
        if (!textarea) return;

        var editor = new EasyMDE({
            element: textarea,
            spellChecker: false,
            status: ['lines', 'words'],
            renderingConfig: { singleLineBreaks: false },
        });

        var currentPostId = opts.postId || '';
        var lastSavedAt = null;
        var debounceTimer = null;
        var DEBOUNCE_MS = 500;

        // Status indicator beneath the editor
        var statusEl = document.createElement('div');
        statusEl.className = 'text-muted small mt-2';
        statusEl.id = 'autosave-status';
        statusEl.textContent = currentPostId ? 'Loaded existing draft' : 'Unsaved';
        textarea.parentNode.appendChild(statusEl);

        var collectState = function() {
            return {
                PostId: currentPostId,
                PostType: opts.postType,
                Title: (document.querySelector('input[name="Title"]') || {}).value || '',
                Content: editor.value(),
                LinkUrl: (document.querySelector('input[name="LinkUrl"]') || {}).value || null,
                Excerpt: (document.querySelector('[name="Excerpt"]') || {}).value || null,
                CoverImageUrl: (document.querySelector('input[name="CoverImageUrl"]') || {}).value || null,
                Tags: (document.getElementById('tags-hidden') || {}).value || '',
            };
        };

        var doAutosave = function() {
            var state = collectState();
            statusEl.textContent = 'Saving…';
            fetch(opts.autosaveEndpoint, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(state),
            })
            .then(function(r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function(resp) {
                if (!currentPostId && resp.postId) {
                    currentPostId = resp.postId;
                    // Replace the URL so a page refresh resumes the draft.
                    var newUrl = window.location.pathname.replace('/new', '/edit/' + resp.postId);
                    if (newUrl !== window.location.pathname) {
                        window.history.replaceState({}, '', newUrl);
                    }
                }
                lastSavedAt = new Date(resp.savedAtUtc);
                statusEl.textContent = 'Saved at ' + lastSavedAt.toLocaleTimeString();
            })
            .catch(function(err) {
                statusEl.textContent = 'Autosave failed (' + err + ')';
            });
        };

        var schedule = function() {
            if (debounceTimer) clearTimeout(debounceTimer);
            debounceTimer = setTimeout(doAutosave, DEBOUNCE_MS);
        };

        editor.codemirror.on('change', schedule);
        ['Title', 'LinkUrl', 'Excerpt', 'CoverImageUrl'].forEach(function(name) {
            var el = document.querySelector('[name="' + name + '"]');
            if (el) el.addEventListener('input', schedule);
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
            tagsInput.addEventListener('input', function() {
                syncTags();
                schedule();
            });
            syncTags();  // initial render
        }

        // Image paste / drop -> upload via /admin/image -> insert markdown image syntax.
        var cm = editor.codemirror;

        function uploadImage(file) {
            var fd = new FormData();
            fd.append('file', file);
            if (currentPostId) fd.append('postId', currentPostId);
            return fetch(opts.uploadEndpoint, {
                method: 'POST',
                credentials: 'same-origin',
                body: fd,
            })
            .then(function(r) { return r.ok ? r.json() : Promise.reject(r.status); });
        }

        function insertImage(file) {
            var altName = file.name || 'image';
            var placeholder = '![Uploading ' + altName + '…]()';
            var doc = cm.getDoc();
            var cursor = doc.getCursor();
            doc.replaceRange(placeholder, cursor);
            uploadImage(file).then(function(resp) {
                var contents = cm.getValue();
                cm.setValue(contents.replace(placeholder, '![' + altName + '](' + resp.url + ')'));
            }).catch(function(err) {
                var contents = cm.getValue();
                cm.setValue(contents.replace(placeholder, '![Upload failed: ' + err + '](#)'));
            });
        }

        cm.on('paste', function(_, ev) {
            if (!ev.clipboardData) return;
            for (var i = 0; i < ev.clipboardData.items.length; i++) {
                var item = ev.clipboardData.items[i];
                if (item.kind === 'file' && item.type.indexOf('image/') === 0) {
                    ev.preventDefault();
                    insertImage(item.getAsFile());
                    return;
                }
            }
        });

        cm.on('drop', function(_, ev) {
            if (!ev.dataTransfer || !ev.dataTransfer.files) return;
            for (var i = 0; i < ev.dataTransfer.files.length; i++) {
                var file = ev.dataTransfer.files[i];
                if (file.type.indexOf('image/') === 0) {
                    ev.preventDefault();
                    insertImage(file);
                    return;
                }
            }
        });

        this._editor = editor;
        this._opts = opts;
        this.forceSave = doAutosave;
    },
};
