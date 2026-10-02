// Просмотр BPMN-диаграмм на публичных страницах (/p/{slug}): bpmn-js NavigatedViewer (панорама и зум).
// Скрипт грузится на всех страницах (он маленький), а сам bpmn-js и его CSS — только когда на странице
// есть контейнер [data-bpmn-view]. После enhanced navigation скрипты страницы не выполняются,
// поэтому контейнеры ищем заново по событию enhancedload.
(function () {
    'use strict';

    var BASE = '/lib/bpmn-js/';
    var VERSION = '18.31.0';
    var loading = null;

    function addCss(href) {
        if (document.querySelector('link[data-bpmn-css="' + href + '"]')) return;
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = BASE + href + '?v=' + VERSION;
        link.setAttribute('data-bpmn-css', href);
        document.head.appendChild(link);
    }

    function loadLib() {
        if (window.BpmnJS) return Promise.resolve();
        if (!loading) {
            loading = new Promise(function (resolve, reject) {
                addCss('assets/diagram-js.css');
                addCss('assets/bpmn-js.css');
                addCss('assets/bpmn-font/css/bpmn.css');
                var s = document.createElement('script');
                s.src = BASE + 'bpmn-navigated-viewer.production.min.js?v=' + VERSION;
                s.onload = resolve;
                s.onerror = function () { loading = null; reject(new Error('Не удалось загрузить bpmn-js')); };
                document.head.appendChild(s);
            });
        }
        return loading;
    }

    function button(label, title, onClick) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-outline-dark btn-sm';
        b.textContent = label;
        b.title = title;
        b.addEventListener('click', onClick);
        return b;
    }

    function showError(root, text) {
        var canvas = root.querySelector('.bpmn-view__canvas');
        if (canvas) canvas.textContent = text;
    }

    function init(root) {
        if (root.querySelector('.djs-container') || root.getAttribute('data-bpmn-busy')) return;
        var source = root.querySelector('script.bpmn-xml');
        var canvas = root.querySelector('.bpmn-view__canvas');
        if (!source || !canvas) return;

        var xml;
        try { xml = JSON.parse(source.textContent); } catch (e) { showError(root, 'Диаграмма повреждена.'); return; }

        root.setAttribute('data-bpmn-busy', '1');
        loadLib().then(function () {
            // за время загрузки контейнер мог исчезнуть (переход на другую страницу)
            if (!root.isConnected) return;
            var viewer = new window.BpmnJS({ container: canvas });
            root._bpmnViewer = viewer;
            return viewer.importXML(xml).then(function () {
                var fit = function () { viewer.get('canvas').zoom('fit-viewport', 'auto'); };
                fit();

                var bar = root.querySelector('.bpmn-view__bar');
                if (bar && !bar.hasChildNodes()) {
                    bar.appendChild(button('Вписать', 'Вписать диаграмму в окно', fit));
                    if (root.requestFullscreen) {
                        bar.appendChild(button('На весь экран', 'Открыть на весь экран', function () {
                            if (document.fullscreenElement === root) document.exitFullscreen();
                            else root.requestFullscreen();
                        }));
                    }
                }
                root.addEventListener('fullscreenchange', function () { setTimeout(fit, 50); });
            });
        }).catch(function (e) {
            showError(root, 'Не удалось показать диаграмму: ' + (e && e.message ? e.message : e));
        }).then(function () {
            root.removeAttribute('data-bpmn-busy');
        });
    }

    function scan() {
        var roots = document.querySelectorAll('[data-bpmn-view]');
        for (var i = 0; i < roots.length; i++) init(roots[i]);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', scan);
    else scan();

    // Blazor.addEventListener доступен после загрузки blazor.web.js, который идёт раньше этого скрипта (defer)
    function hookBlazor() {
        if (window.Blazor && window.Blazor.addEventListener) {
            window.Blazor.addEventListener('enhancedload', scan);
            return true;
        }
        return false;
    }
    if (!hookBlazor()) window.addEventListener('load', hookBlazor);
})();
