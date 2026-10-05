// Загрузка записи созвона (/tasks/calls): XMLHttpRequest, потому что fetch не отдаёт прогресс отправки.
window.lmnCalls = {
    // Берёт файл из <input type="file">, получает antiforgery-токен и грузит на /tasks/calls/upload.
    // Прогресс (целые проценты) шлёт в .NET-метод OnUploadProgress; результат — { id } или { error }.
    upload: async (input, dotnet) => {
        const file = input.files && input.files[0];
        if (!file) return { error: 'Выберите файл.' };

        let token;
        try {
            const r = await fetch('/tasks/calls/antiforgery', { credentials: 'same-origin' });
            if (!r.ok) throw new Error(String(r.status));
            token = await r.json();
        } catch {
            return { error: 'Не удалось получить токен защиты: обновите страницу и войдите заново.' };
        }

        return new Promise(resolve => {
            const xhr = new XMLHttpRequest();
            let last = -1;
            xhr.open('POST', '/tasks/calls/upload');
            xhr.setRequestHeader(token.header, token.token);
            xhr.upload.onprogress = e => {
                if (!e.lengthComputable) return;
                const pct = Math.floor(e.loaded * 100 / e.total);
                if (pct !== last) {
                    last = pct;
                    dotnet.invokeMethodAsync('OnUploadProgress', pct).catch(() => { });
                }
            };
            xhr.onload = () => {
                let body = null;
                try { body = JSON.parse(xhr.responseText); } catch { /* не JSON: например, редирект на вход */ }
                if (xhr.status === 201 && body && body.id) resolve({ id: body.id });
                else resolve({ error: (body && body.error) || `Не удалось загрузить файл (код ${xhr.status}). Возможно, сессия истекла.` });
            };
            xhr.onerror = () => resolve({ error: 'Сетевая ошибка при загрузке.' });
            xhr.onabort = () => resolve({ error: 'Загрузка отменена.' });
            const form = new FormData();
            form.append('file', file);
            xhr.send(form);
        });
    },
    clear: input => { input.value = ''; },
};
