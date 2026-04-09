# 1. Read Before You Touch — Architecture Analysis

### How are the layers organised and what are the dependency rules between them?

Esta aplicação, nopCommerce segue uma arquitetura em camadas, onde as responsabilidades estão bem definidas.
Após uma análise efetuada à estrutura do projeto, podemos então concluir as seguintes divisões:

- ***Nop.Web*** (Camada da apresentação):
	- Camada responsável pela **interação com o utilizador**;
	- Contém os controllers, views, e models (front-end).
	- Componentes principais:
        - **Controllers**: Responsáveis por controlar o fluxo de dados entre a UI e os serviços;
        - **Views**: Exibição da UI;
        - **Models**: Representam dados que vão ser apresentados nas Views;
        - **Plugins**: Algumas funcionalidades adicionais e necessárias.

    - ***Dependência***: Depende da camada ```Nop.Services``` para obter e, posteriormente, poder manipular os dados. 

- ***Nop.Services*** (Camada de Serviços):
    - Contém toda a lógica de negócio, no sentido em que inclui o tratamento das operações como: criar pedidos, adicionar produtos, etc.
    - Componentes principais:
        - **Services**: Tratam da implementação das funcionalidades.

    - ***Dependência***: Depende da camada ```Nop.Data``` para aceder aos dados na BD.

- ***Nop.Data*** (Camada de Dados):
    - Gere o acesso aos dados. Contém repositórios e modelos que interagem diretamente com a BD.
    - Componentes principais:
        - **Repositories**: Tratam das operações de leitura e escrita na BD;
        - **Entities**: Definem as estruturas de dados que são armazenadas na BD (Produtos, Utilizadores, Pedidos, etc.).

    - ***Dependência***: Depende da camada ```Nop.Core``` que define os modelos e a interface.

- ***Nop.Core***: 
    - Camada central do sistema;
    - Contém as interfaces e os modelos de dados utilizados pelas outras camadas.

    - ***Dependência***: Esta é a camada base para as outras camadas. Nesse sentido não depende de nenhuma outra camada


**Conclusão:**
- ***Estrutura de dependências:***
    - Nop.Web → Nop.Services → Nop.Data → Nop.Core
___

| Camada | Responsabilidade | Componentes | Depende de |
|--------|------------------|-------------|------------|
| **Nop.Web** | Apresentação (UI) | Controllers, Views, Models, Factories | `Nop.Services` |
| **Nop.Services** | Lógica de negócio | Services, IEventPublisher | `Nop.Data` |
| **Nop.Data** | Acesso a dados | Repositories, Entities, EF Core | `Nop.Core` |
| **Nop.Core** | Modelos base | Domain Entities, Interfaces, Enums | Nenhuma |

___

### How does nopCommerce handle events internally — what is IEventPublisher and how is it used?

    - O **NopCommerce** implementa um sistema de eventos in-memory baseado no padrão Publisher/Subscriber. Assim, é permitido que diferentes partes da aplicação comuniquem de forma separada, onde os componentes podem publicar eventos sem conhecer quem os vai consumir.

    - ***IEventPublisher***
        - É a interface responsável por publicar eventos no sistema. Atua como mediador central que distribui eventos a todos os consumidores interessados. Basicamente funciona como um ponto de encaminhamento de eventos onde publishers e consumers permanecem completamente separados.

    - ***IConsumer'TEvent'***
        - Esta interface é importante para a criação de subscribers;
        - Para consumir eventos (subscribers), o nopCommerce utiliza a interface ```IConsumer<TEvent>```. Qualquer classe que implemente esta interface é automaticamente invocada quando um evento do tipo TEvent é publicado. 

____

### Where does the code make it easy to add observability, and where does it make it hard?
    - ***Facilidade na adição de observabilidade:***
        - *Injeção de Dependências generalizadas:*
            - Todos os serviços usam DI nos seus construtores, o que permite adicionar ```ILogger'T'``` de forma bastante trivial, sem alterar a lógica existente dos métodos;
        - *Interfaces bem definidas:*
            - As suas definições concisas permitem identificar rapidamente os pontos criticos do fluxo;
            - As interfaces ```IProductService``` e ```IPriceCalculationService``` são bons exemplos. As mesmas foram utilizadas para implementar observabilidade no flow escolhido inicialmente.
        - *Organização do código:*
            - A localização dos Serviços num só namespace ```Nop.Service.Catalog``` facilitou na identificação de todos os componentes relacionados com o fluxo escolhido.

    - ***Dificuldades na adição de observabilidade:***
        - *Métodos bastante grandes com muita lógica conjunta:* 
            - Os métodos contêm centenas de linhas, imensas validações, etc. Isto tornou difícil isolar partes especificas para uma instrumentação mais dividida.
        - *Cache e lógica misturadas:*
            - Alguns métodos, nomeadamente, ```GetFinalPriceAsync```, contêm o respetivo sistema de cślculo de preços dentro de um callback de cache. Pode misturar tempo de execução com tempo de acesso à cache.

_____
### What would you need to change structurally to instrument it properly — and is that change worth making?
    - ***Tornar IEventPublisher extensível:***
        - *O que mudar:*
            - Remover o ```sealed``` da classe ```EventPublisher```
        - *Custo:* - baixo:
            - Alteração pontual numa única classe;
            - Impacto reduzido no código existente;
        - *Benefício:* - alto:
            - Permite criar wrappers com tracing para todos os eventos de uma só vez;
            - Será possível adicionar observabilidade transversal sem modificar cada publisher individualmente.

        Tendo em conta o custo-benefício vale a pena.

    - ***Separar responsabilidade em métodos monolíticos:***
        - *O que mudar:*
            - *'Refactoring'* métodos como o ```SearchProductsAsync``` para separar alguma lógica (Validações, Pesquisas, Filtros, Ordenações, etc.);
            - *Custo:* - alto:  
                - Este *'Refactoring'* pode trazer riscos regressivos;
                - Necessidade imperativa de testes intensivos;
                - Impacto em vários fluxos que dependem deste método.
            - *Benefício:* - alto:
                - Instrumentação mais granular possível;
                - Melhor compreensão do desempenho real do sistema;
                - Spans semânticos para cada operação.

        Tendo em conta o custo-benefício, não é claro chegar a uma conclusão sobre o próprio benefício final desta alteração. Visto que, para uma questão de observabilidade talvez não se justifique totalmente, mas para uma questão de manutenção de código sim. Porém, deve sempre ser feita de forma incremental.
                
    - ***Extrair o que envolve caches e a respetiva lógica nos méetodos para os decorators:***
        - *O que mudar:*
            - Remover camadas de cache dentro dos métodos;
            - Criar decorators que aplicam caching transversalmente.
        - *Custo:* - médio/alto
            - Requer reestruturação de como as caches são aplicadas;
            - Pode impactar, negativamente, a performance se não for bem implementado.
        - *Benefício:* - alto
            - Separação clara entre a lógicas dos respetivos métodos e preocupações transversais;
            - Permite medir tempos reais de execução sem contaminação da cache;
            - Código mais limpo e testável.

        Tendo em conta o custo-benefício, esta seria uma melhoria arquitetural significativa com benefícios para além da observabilidade.
    
    - ***Adicionar suporte nativo ao OpenTelemetry:***
        - *O que mudar:*
            - Incluir pacotes OpenTelemetry como dependências opcionais;
            - Fornecer ```ActivitySources``` pré configurados.
        - *Custo:* - baixo
            - Adicionar pacotes NuGet;
            - Criação de uma nova camada de abstração opcional.
        - *Benefício:* - alto
            - Facilidade em ligar observabilidade sem modificar código.

        Tendo em conta o custo-benefício apresentado, vale muito a pena olhar para esta adição.
        
_____        
### Conclusão:
Após uma análise profunda à arquitetura deste sistema, nopCommerce, concluímos que o mesmo se revela bem estruturado e organizado, seguindo uma arquitetura em camadas, conforme partilhado nos primeiro tópicos desta análise.
Esta organização facilitou, desde logo, a compreensão do fluxo de dados e a identificação dos pontos críticos para as implementações seguintes.

O nopCommerce é, então, um sistema de eventos, com o seu funcionamento baseado em ```IEventPublisher``` e ```IConsumer'T'```. Assim, é permitido que diferentes partes da aplicação reajam a eventos sem dependências diretas. 

Relativamente ao tópico central da adição de observabilidade ao projeto, este sistema contém pontos fortes, assim como algumas desvantagens. 

Ainda assim, as mudanças estruturais propostas ajudariam a melhorar este aspeto.