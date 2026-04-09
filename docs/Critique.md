# Critique

## What in nopCommerce's design helped or hindered your instrumentation work?

### O que facilitou:

A arquitetura deste sistema apresentou vários pontos que facilitaram todo o trabalho de instrumentação:

- **Injeção de Dependência generalizada**: Todos os serviços utilizam DI nos construtores, o que permitiu adicionar `ILogger<T>` de forma praticamente trivial, sem alterar a lógica existente, basicamente apenas extendendo os construtores.

- **Interfaces bem definidas**: A correta implementação e organização das interfaces permitiu identificar rapidamente os pontos críticos do fluxo. Um exemplo disso são as interfaces ```IProductService``` e ```IPriceCalculationService```.

- **Organização do código**: A localização dos serviços, e não só, no namespace ```Nop.Services.Catalog``` facilitou a identificação de todos os componentes relacionados com o fluxo.

### O que dificultou:

Apesar dos pontos identificados no tópico anterior, também foram encontradas algumas dificulades:

- **Métodos monolíticos**: Muitos dos métodos relevantes contêm centenas de linhas com múltiplas responsabilidades (validação, cache, filtros, ordenação). Um exemplo disso é o método ```SearchProductsAsync```.

- **Cache misturada com lógica do método**: Em alguns métodos, como o ```GetFinalPriceAsync```, a lógica de negócio encontrava-se dentro de callbacks de cache.
Assim, tornava-se mais difícil medições precisas, pois os tempos de execução e os tempos de acesso à cache estão misturados.

- **Falta de suporte nativo a OpenTelemetry**: O nopCommerce não inclui qualquer `ActivitySource` pré-definido, obrigando à adição manual em cada serviço instrumentado. 
Considerando que, neste caso especifico, cada serviço contém bastantes classes, poderia ser uma boa abordagem a sua disponibilidade por padrão.

---

## If you were making architectural decisions on this project going forward, what would you change to make it more observable — and at what cost?

Se pudesse tomar decisões arquiteturais neste projeto, proporia as seguintes alterações:

### 1. Adicionar ActivitySource estático em todos os serviços

- **Mudança**: Adicionar um ```ActivitySource``` estático em cada classe nos serviços.

- **Custo**: Médio (trabalho e tempo a dispensar).

- **Benefício**: Tracing consistente em todo o sistema, sem necessidade de intervenção manual futura.

### 2. Separação de responsabilidades nos métodos

- **Mudança**: *'Refactoring'* de métodos como ```SearchProductsAsync``` para separar validação, cache, filtros, etc. em métodos menores e mais focados.

- **Custo**: Alto (risco de regressões, necessidade de testes intensivos).

- **Benefício**: 
    - Instrumentação granular possível:
    - Melhor compreensão do desempenho real do sistema;
    - Código mais testável e mantível.

### 3. Extrair a lógica da cache

- **Mudança**: Remover as chamadas de cache de dentro dos métodos e criar decorators que aplicam caching transversalmente.

- **Custo**: Médio/Alto (requer reestruturação de como o cache é aplicado).

- **Benefício**: Separação clara entre lógica de negócio e preocupações transversais, permitindo medir tempos reais de execução sem contaminação da cache.

### 4. Adicionar suporte nativo a OpenTelemetry

- **Mudança**: Incluir pacotes OpenTelemetry como dependências opcionais e fornecer ```ActivitySource``` pré-configurados.

- **Custo**: Baixo (adicionar pacotes NuGet e criar camada de abstração).

- **Benefício**: Facilidade em ligar observabilidade sem modificar código.

---

## Where did you have to make a surgical change to the existing code? Why was it necessary, and how did you minimise the impact?

Durante a instrumentação do fluxo de pesquisa e visualização de produtos, foi necessário realizar algumas *alterações cirúrgicas* no código existente:

### 1. Adição de ILogger<T> nos construtores

**Onde**: `ProductService`, `PriceCalculationService`, `ProductModelFactory`, `CatalogController`.

**Porquê**: Necessário para adicionar logging estruturado sem alterar toda a lógica existente.

**Como foi minimizado o impacto**: Os construtores foram estendidos com um novo parâmetro, respeitando a injeção de dependência já existente. O container DI trata automaticamente da resolução, sem qualquer alteração nos consumidores destes serviços.

### 2. Adição manual de ActivitySource

**Onde**: Em todas as classes instrumentadas relativas ao fluxo.

**Porquê**: Como já abordámos, o nopCommerce não fornece ActivitySource nativo, e era necessário para criar spans de tracing.

**Como foi minimizado o impacto**: Criação do `ActivitySource` como `static readonly` no topo de cada classe.

### 3. Injeção de spans em métodos existentes

**Onde**: `GetProductByIdAsync`, `SearchProductsAsync`, `GetFinalPriceAsync`, `PrepareProductDetailsModelAsync`, etc.

**Porquê**: Para capturar o fluxo completo desde o HTTP até à base de dados.

**Como foi minimizado o impacto**: Foram envolvidos apenas os blocos de código existentes com `using var activity = ...`, sem modificar a sua lógica interna. Nos casos de exceção, foi adicionado o tratamento de erros onde marcamos o span como erro, mas sempre após relançar a exceção para não alterar o comportamento original.

### 4. Adição de métricas customizadas

**Onde**: `ProductService` e `PriceCalculationService`.

**Porquê**: Para cumprir o requisito de métricas operacionalmente úteis.

**Como foi minimizado o impacto**: Criação e definição da classe `TelemetryMetrics` existente ```Nop.Core.Observability.TelemetryMetrics.cs```, registando, posteriormente, as métricas apenas nos pontos de saída dos métodos, sem interferir com os valores de retorno ou com a lógica do respetivo método.

---

## Conclusão

Esta instrumentação no nopCommerce demonstrou que, embora o sistema tenha uma arquitetura sólida e bem organizada, a falta de alguns fatores identificados tornou o processo mais demorado e trabalhoso. Ainda assim, as mudanças realizadas permitiram adicionar tracing, metrics e logging de forma eficaz, sem comprometer a estabilidade do sistema.