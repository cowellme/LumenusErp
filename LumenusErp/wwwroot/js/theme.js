// Тёмная тема: переключатель в шапке, выбор в localStorage ('theme': 'light' | 'dark').
// Первичную установку делает inline-скрипт в <head> App.razor (до CSS, без мигания);
// здесь — клик по [data-theme-toggle], слежение за системной темой и повторное применение
// после enhanced navigation Blazor, которая может сбросить атрибуты <html>.
(function () {
    'use strict';

    var KEY = 'theme';
    var media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

    function stored() {
        try {
            var v = localStorage.getItem(KEY);
            return v === 'light' || v === 'dark' ? v : null;
        } catch (e) { return null; }
    }

    function current() {
        return stored() || (media && media.matches ? 'dark' : 'light');
    }

    function apply(theme) {
        var d = document.documentElement;
        if (d.getAttribute('data-theme') !== theme) d.setAttribute('data-theme', theme);
        if (d.getAttribute('data-bs-theme') !== theme) d.setAttribute('data-bs-theme', theme);
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', theme === 'dark' ? '#0b0b0c' : '#ffffff');
    }

    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('[data-theme-toggle]')) return;
        var next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem(KEY, next); } catch (err) { }
        apply(next);
    });

    // Пока выбора нет — следуем системной теме.
    if (media) {
        var onSystemChange = function () { if (!stored()) apply(current()); };
        if (media.addEventListener) media.addEventListener('change', onSystemChange);
        else if (media.addListener) media.addListener(onSystemChange);
    }

    function hookBlazor() {
        if (!window.Blazor || !window.Blazor.addEventListener) return false;
        window.Blazor.addEventListener('enhancedload', function () { apply(current()); });
        return true;
    }

    // blazor.web.js грузится раньше (без defer), но Blazor может появиться чуть позже — ждём.
    function waitForBlazor(left) {
        if (hookBlazor() || left <= 0) return;
        setTimeout(function () { waitForBlazor(left - 1); }, 100);
    }

    apply(current());
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { waitForBlazor(100); });
    } else {
        waitForBlazor(100);
    }
})();
