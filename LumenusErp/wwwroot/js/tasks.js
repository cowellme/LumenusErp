// Помощники страниц трекера (/tasks): часовой пояс браузера и копирование в буфер обмена.
window.lmnTasks = {
    // Смещение локального времени от UTC в минутах (в отличие от getTimezoneOffset — со знаком «+ на восток»).
    utcOffsetMinutes: () => -new Date().getTimezoneOffset(),
    copy: async (text) => {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },
};
