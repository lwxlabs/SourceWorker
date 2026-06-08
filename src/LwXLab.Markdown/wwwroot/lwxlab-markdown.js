window.lwxlabMarkdown = window.lwxlabMarkdown || {};
window.lwxlabMarkdown.instances = window.lwxlabMarkdown.instances || {};

let monacoLoaderPromise = null;
let mermaidPromise = null;
let highlightPromise = null;
let highlightCssLoaded = false;

// Helpers
async function loadMonaco(loaderUrl, baseUrl) {
    if (window.monaco) return window.monaco;
    if (monacoLoaderPromise) return monacoLoaderPromise;

    monacoLoaderPromise = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = loaderUrl;
        script.onload = () => {
            require.config({ paths: { 'vs': baseUrl } });
            require(['vs/editor/editor.main'], () => resolve(window.monaco));
        };
        script.onerror = reject;
        document.head.appendChild(script);
    });
    return monacoLoaderPromise;
}

function ensureMonacoThemes() {
    if (!window.monaco || window.lwxlabMarkdown._themesRegistered) return;

    window.monaco.editor.defineTheme("lwxlab-gray", {
        base: "vs",
        inherit: true,
        rules: [],
        colors: {
            "editor.background": "#f3f4f6",
            "editor.foreground": "#1f2937",
            "editorLineNumber.foreground": "#64748b",
            "editorCursor.foreground": "#111827",
            "editor.selectionBackground": "#cbd5e1",
            "editor.inactiveSelectionBackground": "#e2e8f0",
            "editor.lineHighlightBackground": "#e5e7eb"
        }
    });

    window.lwxlabMarkdown._themesRegistered = true;
}

async function runHighlight(preview, options) {
    if (!window.hljs) {
        if (!highlightPromise) {
            highlightPromise = new Promise((resolve) => {
                const script = document.createElement('script');
                script.src = options.highlightUrl;
                script.onload = () => resolve();
                document.head.appendChild(script);
            });
            if (!highlightCssLoaded) {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = options.highlightThemeUrl;
                document.head.appendChild(link);
                highlightCssLoaded = true;
            }
        }
        await highlightPromise;
    }
    preview.querySelectorAll('pre code').forEach(block => window.hljs.highlightElement(block));
}

async function runMermaid(preview, options) {
    if (!window.mermaid) {
        if (!mermaidPromise) {
            mermaidPromise = import(options.mermaidUrl)
                .then(module => {
                    window.mermaid = module.default || module;
                    window.mermaid.initialize({ startOnLoad: false });
                    return window.mermaid;
                })
                .catch(err => {
                    console.error("Failed to load mermaid:", err);
                    return null;
                });
        }
        await mermaidPromise;
    }

    if (window.mermaid) {
        await window.mermaid.run({ querySelector: '.mermaid', nodes: preview.querySelectorAll('.mermaid') });
    }
}

// Registry-based init
window.lwxlabMarkdown.init = async function (instanceId, editorId, previewId, dotNetRef, options) {
    if (window.lwxlabMarkdown.instances[instanceId]) {
        window.lwxlabMarkdown.dispose(instanceId);
    }

    const textarea = document.getElementById(editorId);
    if (!textarea) return;

    const instance = {
        id: instanceId,
        dotNetRef: dotNetRef,
        editor: null,
        subscriptions: [],
        debounceTimers: {},
        timers: [],
        disposed: false,
        options: options || {}
    };

    window.lwxlabMarkdown.instances[instanceId] = instance;

    if (options.enableMonaco) {
        try {
            const monaco = await loadMonaco(options.monacoLoaderUrl, options.monacoBaseUrl);
            ensureMonacoThemes();
            const wrapper = document.createElement('div');
            wrapper.className = 'lwx-md-monaco';
            textarea.parentNode.insertBefore(wrapper, textarea.nextSibling);
            textarea.style.display = 'none';

            const editor = monaco.editor.create(wrapper, {
                value: textarea.value || '',
                language: options.editor.language || 'markdown',
                theme: options.editor.theme || 'vs',
                automaticLayout: options.editor.automaticLayout !== false,
                lineNumbers: options.editor.lineNumbers || 'on',
                wordWrap: options.editor.wordWrap || 'on',
                minimap: options.editor.minimap || { enabled: false },
                tabSize: options.editor.tabSize || 2,
                wordBasedSuggestions: options.editor.wordBasedSuggestions || 'allDocuments',
                formatOnPaste: !!options.editor.formatOnPaste,
                formatOnType: !!options.editor.formatOnType,
                renderWhitespace: options.editor.renderWhitespace || 'none',
                scrollBeyondLastLine: !!options.editor.scrollBeyondLastLine,
                stickyScroll: options.editor.stickyScroll || { enabled: false },
                fontFamily: options.editor.fontFamily,
                fontSize: options.editor.fontSize,
                readOnly: !!options.editor.readOnly
            });

            instance.editor = editor;

            const subscription = editor.onDidChangeModelContent(() => {
                const current = window.lwxlabMarkdown.instances[instanceId];
                if (!current || current.disposed) return;
                const value = editor.getValue();
                window.lwxlabMarkdown.debounce(instanceId, "valueChanged", () => {
                    window.lwxlabMarkdown.invokeBlazor(instanceId, "NotifyValueChanged", value);
                }, current.options.previewDebounce || 250);
            });

            instance.subscriptions.push(subscription);
        } catch (error) {
            console.warn('LwXLab.Markdown: Monaco failed, using textarea fallback.', error);
            textarea.style.display = '';
        }
    }

    await window.lwxlabMarkdown.enhancePreview(instanceId, previewId, options);
};

window.lwxlabMarkdown.enhancePreview = async function (instanceId, previewId, options) {
    const preview = document.getElementById(previewId);
    if (!preview) return;

    if (options.enableHighlight) {
        await runHighlight(preview, options);
    }

    if (options.enableMermaid) {
        await runMermaid(preview, options);
    }
};

window.lwxlabMarkdown.invokeBlazor = async function (instanceId, methodName, ...args) {
    const instance = window.lwxlabMarkdown.instances[instanceId];
    if (!instance || instance.disposed || !instance.dotNetRef) return;

    try {
        await instance.dotNetRef.invokeMethodAsync(methodName, ...args);
    } catch (error) {
        const message = String(error?.message || error);
        console.warn("LwXLab.Markdown JS interop callback failed:", message);
        if (message.includes("disposed") || message.includes("There is no tracked object") || message.includes("dotNetObjectId")) {
            instance.disposed = true;
            window.lwxlabMarkdown.dispose(instanceId);
        }
    }
};

window.lwxlabMarkdown.debounce = function (instanceId, key, callback, delay) {
    const instance = window.lwxlabMarkdown.instances[instanceId];
    if (!instance || instance.disposed) return;

    if (instance.debounceTimers[key]) {
        clearTimeout(instance.debounceTimers[key]);
    }

    instance.debounceTimers[key] = setTimeout(() => {
        if (!instance.disposed) callback();
    }, delay || 250);
};

window.lwxlabMarkdown.dispose = function (instanceId) {
    const instance = window.lwxlabMarkdown.instances[instanceId];
    if (!instance) return;

    instance.disposed = true;

    Object.keys(instance.debounceTimers).forEach(key => clearTimeout(instance.debounceTimers[key]));
    instance.debounceTimers = {};

    instance.timers.forEach(timer => clearTimeout(timer));
    instance.timers = [];

    instance.subscriptions.forEach(sub => { try { sub.dispose?.(); } catch {} });
    instance.subscriptions = [];

    if (instance.editor) {
        try { instance.editor.dispose(); } catch {}
        instance.editor = null;
    }

    instance.dotNetRef = null;
    delete window.lwxlabMarkdown.instances[instanceId];
};

window.lwxlabMarkdown.setTheme = async function (instanceId, monacoTheme, mermaidTheme) {
    const instance = window.lwxlabMarkdown.instances?.[instanceId];

    if (!instance || instance.disposed) {
        return;
    }

    instance.options = instance.options || {};
    instance.options.monacoTheme = monacoTheme;
    instance.options.mermaidTheme = mermaidTheme;

    if (window.monaco) {
        ensureMonacoThemes();

        if (instance.editor) {
            window.monaco.editor.setTheme(monacoTheme || "vs");
        }
    }

    if (window.mermaid) {
        window.mermaid.initialize({
            startOnLoad: false,
            theme: mermaidTheme || "default",
            securityLevel: "loose"
        });

        window.lwxlabMarkdown._mermaidInitialized = true;
    }

    await window.lwxlabMarkdown.refresh(instanceId);
};

window.lwxlabMarkdown.startSplitDrag = function (
    instanceId,
    direction,
    currentRatio,
    minEditorPercent,
    minPreviewPercent
) {
    const instance = window.lwxlabMarkdown.instances?.[instanceId];
    const root = document.getElementById(instanceId);

    if (!instance || instance.disposed || !root) {
        return;
    }

    const body = root.querySelector(".lwx-md-body");

    if (!body) {
        return;
    }

    const isHorizontal = direction === "Horizontal";
    const rect = body.getBoundingClientRect();
    let latestRatio = currentRatio || 50;

    document.body.classList.add(
        "lwx-md-is-resizing",
        isHorizontal ? "lwx-md-is-resizing-horizontal" : "lwx-md-is-resizing-vertical"
    );

    const onMove = (event) => {
        event.preventDefault();
        const size = isHorizontal ? rect.height : rect.width;

        if (!size || size <= 0) {
            return;
        }

        const offset = isHorizontal
            ? event.clientY - rect.top
            : event.clientX - rect.left;

        let ratio = (offset / size) * 100;

        ratio = Math.max(minEditorPercent || 20, ratio);
        ratio = Math.min(100 - (minPreviewPercent || 20), ratio);
        latestRatio = ratio;

        body.style.setProperty("--lwx-md-split-ratio", `${ratio}%`);

        if (instance.editor && window.monaco) {
            window.requestAnimationFrame(() => {
                try {
                    instance.editor.layout();
                } catch {
                }
            });
        }

    };

    const onUp = (event) => {
        event.preventDefault();
        document.body.classList.remove(
            "lwx-md-is-resizing",
            "lwx-md-is-resizing-horizontal",
            "lwx-md-is-resizing-vertical"
        );

        window.removeEventListener("pointermove", onMove);
        window.removeEventListener("pointerup", onUp);

        window.lwxlabMarkdown.invokeBlazor(instanceId, "NotifySplitRatioChanged", latestRatio);

        if (instance.editor && window.monaco) {
            setTimeout(() => {
                try {
                    instance.editor.layout();
                } catch {
                }
            }, 0);
        }

        if (window.lwxlabMarkdown.refresh) {
            window.lwxlabMarkdown.refresh(instanceId);
        }
    };

    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
};

window.lwxlabMarkdown.layout = function (instanceId) {
    const instance = window.lwxlabMarkdown.instances?.[instanceId];

    if (!instance || instance.disposed) {
        return;
    }

    if (instance.editor && window.monaco) {
        setTimeout(() => {
            try {
                instance.editor.layout();
            } catch {
            }
        }, 0);
    }
};

window.lwxlabMarkdown.refresh = async function (instanceId) {
    const instance = window.lwxlabMarkdown.instances?.[instanceId];
    if (!instance || instance.disposed) return;

    window.lwxlabMarkdown.layout(instanceId);

    const preview = document.getElementById(`${instanceId}-preview`);
    if (preview) {
        await window.lwxlabMarkdown.enhancePreview(instanceId, preview.id, instance.options || {});
    }
};

window.lwxlabMarkdown.setEditorValue = function (instanceId, editorId, value) {
    const instance = window.lwxlabMarkdown.instances[instanceId];
    if (instance && instance.editor) {
        if (instance.editor.getValue() !== value) {
            const position = instance.editor.getPosition();
            instance.editor.setValue(value || '');
            if (position) instance.editor.setPosition(position);
        }
        return;
    }

    const textarea = document.getElementById(editorId);
    if (textarea && textarea.value !== value) textarea.value = value || '';
};

window.lwxlabMarkdown.insertText = function (instanceId, editorId, text) {
    const instance = window.lwxlabMarkdown.instances[instanceId];
    if (instance && instance.editor) {
        const selection = instance.editor.getSelection();
        instance.editor.executeEdits('lwx-md-toolbar', [{ range: selection, text: text || '', forceMoveMarkers: true }]);
        instance.editor.focus();
        return;
    }

    const textarea = document.getElementById(editorId);
    if (!textarea) return;

    const start = textarea.selectionStart || textarea.value.length;
    const end = textarea.selectionEnd || textarea.value.length;
    textarea.value = textarea.value.substring(0, start) + text + textarea.value.substring(end);
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    textarea.focus();
};

window.lwxlabMarkdown.saveAutoSave = function (id, value) {
    if (!id) return;
    window.localStorage.setItem(`lwx-md-autosave:${id}`, value || '');
};

window.lwxlabMarkdown.restoreAutoSave = function (id) {
    if (!id) return null;
    return window.localStorage.getItem(`lwx-md-autosave:${id}`);
};

window.lwxlabMarkdown.clearAutoSave = function (id) {
    if (!id) return;
    window.localStorage.removeItem(`lwx-md-autosave:${id}`);
};
