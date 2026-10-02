using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LumenusErp.Services;

/// <summary>Проверка BPMN 2.0 XML и извлечение названий элементов (для llms-full.txt).</summary>
public static class BpmnXml
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public static readonly XNamespace ModelNs = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    /// <summary>Пустая диаграмма для нового блока: один стартовый элемент.</summary>
    public const string EmptyDiagram = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" id="Definitions_1" targetNamespace="http://bpmn.io/schema/bpmn">
          <bpmn:process id="Process_1" isExecutable="false">
            <bpmn:startEvent id="StartEvent_1" />
          </bpmn:process>
          <bpmndi:BPMNDiagram id="BPMNDiagram_1">
            <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
              <bpmndi:BPMNShape id="StartEvent_1_di" bpmnElement="StartEvent_1">
                <dc:Bounds x="179" y="159" width="36" height="36" />
              </bpmndi:BPMNShape>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn:definitions>
        """;

    /// <summary>Возвращает текст ошибки или null, если XML корректен и это BPMN.</summary>
    public static string? Validate(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return "Диаграмма пуста.";
        }
        if (Encoding.UTF8.GetByteCount(xml) > MaxBytes)
        {
            return $"Диаграмма больше {MaxBytes / 1024 / 1024} МБ.";
        }
        try
        {
            var doc = Parse(xml);
            if (doc.Root?.Name != ModelNs + "definitions")
            {
                return "Это не BPMN 2.0: корневой элемент должен быть definitions в пространстве имён BPMN.";
            }
        }
        catch (XmlException ex)
        {
            return "Файл не является корректным XML: " + ex.Message;
        }
        return null;
    }

    /// <summary>Названия элементов процесса (задачи, события, шлюзы, участники, подписи потоков) в порядке документа.</summary>
    public static List<string> ExtractNames(string xml)
    {
        try
        {
            return Parse(xml).Descendants()
                .Where(e => e.Name.Namespace == ModelNs)
                .Select(e => ((string?)e.Attribute("name"))?.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .ToList();
        }
        catch (XmlException)
        {
            return [];
        }
    }

    // DTD запрещён и внешние ресурсы не загружаются: защита от XXE и «взрыва» сущностей
    private static XDocument Parse(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }
}
