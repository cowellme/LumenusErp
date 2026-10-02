// Редактор BPMN в админке: bpmn-js Modeler. ES-модуль, подключается из BpmnEditor.razor через JS interop.
// Сам bpmn-js (глобальный BpmnJS) и его CSS подгружаются при первом create.
const BASE = '/lib/bpmn-js/';
const VERSION = '18.31.0';
let loading = null;

function addCss(href) {
    if (document.querySelector(`link[data-bpmn-css="${href}"]`)) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = `${BASE}${href}?v=${VERSION}`;
    link.setAttribute('data-bpmn-css', href);
    document.head.appendChild(link);
}

function loadLib() {
    if (window.BpmnJS) return Promise.resolve();
    if (!loading) {
        loading = new Promise((resolve, reject) => {
            addCss('assets/diagram-js.css');
            addCss('assets/bpmn-js.css');
            addCss('assets/bpmn-font/css/bpmn.css');
            const s = document.createElement('script');
            s.src = `${BASE}bpmn-modeler.production.min.js?v=${VERSION}`;
            s.onload = resolve;
            s.onerror = () => { loading = null; reject(new Error('Не удалось загрузить bpmn-js')); };
            document.head.appendChild(s);
        });
    }
    return loading;
}

// Создаёт модельер в элементе element и возвращает объект-handle с методами getXml/setXml/fit/download/destroy.
export async function create(element, xml) {
    await loadLib();
    const modeler = new window.BpmnJS({ container: element });
    try {
        await modeler.importXML(xml);
    } catch (e) {
        modeler.destroy();
        throw new Error(e && e.message ? e.message : String(e));
    }
    const fit = () => modeler.get('canvas').zoom('fit-viewport', 'auto');
    fit();

    return {
        async getXml() {
            const { xml } = await modeler.saveXML({ format: true });
            return xml;
        },
        async setXml(newXml) {
            await modeler.importXML(newXml);
            fit();
        },
        fit,
        async download(fileName) {
            const { xml } = await modeler.saveXML({ format: true });
            const url = URL.createObjectURL(new Blob([xml], { type: 'application/xml' }));
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        },
        destroy() {
            modeler.destroy();
        },
    };
}
