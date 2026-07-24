using EME.Diagnostics.Core.Models;

namespace EME.Diagnostics.Services;

public sealed class StressCatalogService
{
    public IReadOnlyList<StressTestDefinition> GetDefinitions() =>
    [
        new(StressTarget.Cpu, "CPU", "Carga sustentada para validar temperatura, clock e estabilidade.", TimeSpan.FromMinutes(30)),
        new(StressTarget.Gpu, "GPU", "Carga compute DirectX 11 nativa para validar estabilidade, temperatura e driver gráfico.", TimeSpan.FromMinutes(30)),
        new(StressTarget.Memory, "RAM", "Validação de memória e estabilidade do controlador.", TimeSpan.FromMinutes(30)),
        new(StressTarget.Storage, "Storage", "Carga controlada de leitura e escrita com limites de segurança.", TimeSpan.FromMinutes(15)),
        new(StressTarget.Combined, "Combined Test", "Orquestração futura de CPU, GPU, RAM e armazenamento.", TimeSpan.FromHours(1))
    ];
}
