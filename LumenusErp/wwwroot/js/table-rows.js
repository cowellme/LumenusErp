// Раскрытие строк реестра проектов без цепи Blazor: делегирование на document,
// поэтому работает и после enhanced navigation (скрипты страниц при ней не выполняются).
(function () {
    function toggle(row) {
        var detail = document.getElementById(row.getAttribute('aria-controls'));
        if (!detail) return;
        var open = row.getAttribute('aria-expanded') !== 'true';
        row.setAttribute('aria-expanded', open ? 'true' : 'false');
        row.classList.toggle('row-open', open);
        detail.hidden = !open;
    }

    function rowOf(target) {
        var row = target.closest('tr[data-row-toggle]');
        // клики по ссылкам внутри строки — обычная навигация
        return row && !target.closest('a') ? row : null;
    }

    document.addEventListener('click', function (e) {
        var row = rowOf(e.target);
        if (row) toggle(row);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        var row = rowOf(e.target);
        if (!row || e.target !== row) return;
        e.preventDefault();
        toggle(row);
    });
})();
