// Помощники English Studio (/english): озвучивание слов и примеров, прокрутка чата вниз.
window.lmnEnglish = {
    // Озвучка через Web Speech API браузера (британский вариант); без поддержки — тихо ничего не делает.
    speak: (text) => {
        if (!text || !('speechSynthesis' in window)) return false;
        const u = new SpeechSynthesisUtterance(text);
        u.lang = 'en-GB';
        speechSynthesis.cancel();
        speechSynthesis.speak(u);
        return true;
    },
    scrollToEnd: (el) => {
        if (el) el.scrollTop = el.scrollHeight;
    },
};

// Кнопки с data-es-speak работают и на статических, и на интерактивных страницах (делегирование).
document.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-es-speak]');
    if (!btn) return;
    e.preventDefault();
    window.lmnEnglish.speak(btn.getAttribute('data-es-speak'));
});
