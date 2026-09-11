using System.Collections.Generic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Demonstracao visual do algoritmo de Dijkstra em uma grade 2D no plano XZ.
// A cada passo, o algoritmo expande o no alcancavel de menor custo acumulado.
public class AIDijkstra : MonoBehaviour
{
    // Representa uma celula da grade e os dados temporarios calculados pela busca.
    private class Node
    {
        // Indices da celula na matriz nodes.
        public int x, y;
        // Custo para entrar no no. int.MaxValue significa que ele e um obstaculo.
        public int cost;
        // G: menor custo conhecido desde a origem ate este no.
        public int g = int.MaxValue;
        // No imediatamente anterior na melhor rota conhecida ate este no.
        public Node parent;
        // Estado usado exclusivamente para desenhar o andamento nos Gizmos.
        public State state;
    }

    // None: ainda nao descoberto; Open: aguardando processamento;
    // Closed: ja processado; Path: pertence ao caminho final encontrado.
    private enum State { None, Open, Closed, Path }

    // Largura e profundidade de cada celula no mundo Unity.
    public float nodeSize = 1f;
    // Quantidade de celulas da grade nos eixos X e Z.
    public Vector2Int size = new Vector2Int(10, 10);
    // Camadas que representam objetos que bloqueiam a passagem.
    [SerializeField] private LayerMask obstacleMask;
    // Altura da caixa usada para detectar os obstaculos.
    [SerializeField] private float obstacleHeight = 1f;
    // Transform do inicio. Se vazio, o objeto deste componente sera usado.
    [SerializeField] private Transform startPoint;
    // Transform do destino. Se vazio, o objeto deste componente sera usado.
    [SerializeField] private Transform targetPoint;
    // Objeto que sera movido pelo caminho encontrado. Se vazio, usa este objeto.
    [SerializeField] private Transform player;
    // Velocidade de movimento do jogador entre os centros dos nos.
    [SerializeField] private float playerMoveSpeed = 3f;
    // Quando ativo, executa um passo por frame, sem pressionar Espaco.
    [SerializeField] private bool automatic;
    // Define se a grade e os estados da busca serao exibidos na Scene.
    [SerializeField] private bool showGridGizmos = true;

    // Matriz que armazena todos os nos da grade.
    private Node[,] nodes;
    // Conjunto de nos descobertos, mas ainda nao processados pelo algoritmo.
    private readonly List<Node> openNodes = new List<Node>();
    // Referencia ao destino para comparar durante cada passo.
    private Node targetNode;
    // Caminho final organizado da origem ate o destino.
    private readonly List<Node> pathNodes = new List<Node>();
    // Referencia da coroutine para impedir dois movimentos ao mesmo tempo.
    private Coroutine playerMovement;
    // Impede processamento apos encontrar um caminho ou concluir que nao existe rota.
    private bool finished;

    void Start()
    {
        // Primeiro identificamos a grade e os obstaculos; depois iniciamos a busca.
        CreateGrid();
        ResetSearch();
    }

    void Update()
    {
        // Espaco mostra uma expansao do algoritmo por vez.
        if (Input.GetKey(KeyCode.Space)) StepSearch();
        // R apaga a execucao atual e permite iniciar outra demonstracao.
        if (Input.GetKeyDown(KeyCode.R)) ResetSearch();
        // No modo automatico, ainda e executado apenas um passo em cada frame.
        if (automatic && !finished) StepSearch();
    }

    void CreateGrid()
    {
        // A matriz usa os mesmos indices X/Y empregados no AIMove original.
        nodes = new Node[size.x, size.y];
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            {
                // Cria o no e verifica se ha algum Collider de obstaculo nesta celula.
                Node node = new Node();
                node.x = x;
                node.y = y;
                // Nos livres custam 1; obstaculos nunca podem ser selecionados.
                if (IsWalkable(NodeToWorld(x, y)))
                {
                    node.cost = 1;
                }
                else
                {
                    node.cost = int.MaxValue;
                }
                nodes[x, y] = node;
            }
    }

    [ContextMenu("Reiniciar busca")]
    public void ResetSearch()
    {
        // Nao ha busca possivel sem uma grade valida.
        if (nodes == null || size.x <= 0 || size.y <= 0) return;
        // Remove todos os candidatos descobertos na execucao anterior.
        openNodes.Clear();
        // Remove o caminho e interrompe um movimento da execucao anterior.
        pathNodes.Clear();
        if (playerMovement != null)
        {
            StopCoroutine(playerMovement);
            playerMovement = null;
        }
        finished = false;

        // Limpa os valores que Dijkstra calculou na execucao anterior.
        for (int x = 0; x < size.x; x++)
        for (int y = 0; y < size.y; y++)
        {
            // Inicialmente nao conhecemos nenhum caminho ate os nos.
            nodes[x, y].g = int.MaxValue;
            nodes[x, y].parent = null;
            nodes[x, y].state = State.None;
        }

        // Converte as posicoes dos objetos do mundo para celulas da grade.
        Vector3 startPosition = transform.position;
        if (startPoint != null)
        {
            startPosition = startPoint.position;
        }
        Node start = WorldToNode(startPosition);

        Vector3 targetPosition = transform.position;
        if (targetPoint != null)
        {
            targetPosition = targetPoint.position;
        }
        targetNode = WorldToNode(targetPosition);
        // Uma busca nao pode iniciar ou terminar em uma celula bloqueada.
        if (start.cost == int.MaxValue || targetNode.cost == int.MaxValue)
        {
            finished = true;
            return;
        }

        // O custo para estar no proprio ponto inicial e zero.
        start.g = 0;
        start.state = State.Open;
        // A lista aberta comeca contendo somente a origem.
        openNodes.Add(start);
    }

    [ContextMenu("Proximo passo")]
    public void StepSearch()
    {
        // Nao altera um resultado ja concluido.
        if (finished) return;

        // Esta e a regra central de Dijkstra: menor G primeiro.
        Node current = GetLowestCostOpenNode();
        // Se a lista aberta esvaziou, todas as rotas possiveis foram tentadas.
        if (current == null)
        {
            finished = true;
            Debug.Log("Nao existe caminho.", this);
            return;
        }

        // O no escolhido deixa de aguardar e passa a ser definitivamente processado.
        openNodes.Remove(current);
        current.state = State.Closed;
        // O primeiro destino removido da lista aberta possui o menor custo possivel.
        if (current == targetNode)
        {
            finished = true;
            CreatePath();
            MovePlayerAlongPath();
            return;
        }

        foreach (Node neighbor in GetNeighbors(current))
        {
            // Ignora paredes e nos cujo menor custo ja foi confirmado.
            if (neighbor.cost == int.MaxValue || neighbor.state == State.Closed) continue;
            // Calcula o custo de chegar ao vizinho passando pelo no atual.
            int newCost = current.g + neighbor.cost;
            // Mantem a rota antiga se ela ja for melhor ou igual.
            if (newCost >= neighbor.g) continue;

            // Registra uma rota mais barata ate o vizinho.
            neighbor.g = newCost;
            neighbor.parent = current;
            // O no passa a ser candidato a uma proxima expansao.
            if (neighbor.state != State.Open)
            {
                neighbor.state = State.Open;
                openNodes.Add(neighbor);
            }
        }
    }

    Node GetLowestCostOpenNode()
    {
        // Procura linearmente o menor G na lista aberta.
        // Em projetos maiores, uma fila de prioridade pode substituir esta lista.
        Node best = null;
        foreach (Node node in openNodes)
            // Dijkstra nao utiliza distancia ao destino nem heuristica: somente G.
            if (best == null || node.g < best.g) best = node;
        return best;
    }

    IEnumerable<Node> GetNeighbors(Node node)
    {
        // Retorna somente vizinhos ortogonais dentro dos limites da grade.
        if (node.x > 0) yield return nodes[node.x - 1, node.y];
        if (node.x + 1 < size.x) yield return nodes[node.x + 1, node.y];
        if (node.y > 0) yield return nodes[node.x, node.y - 1];
        if (node.y + 1 < size.y) yield return nodes[node.x, node.y + 1];
    }

    void CreatePath()
    {
        // Volta do destino ate a origem usando os parents gravados durante a busca.
        for (Node node = targetNode; node != null; node = node.parent)
        {
            node.state = State.Path;
            pathNodes.Add(node);
        }

        // O loop anterior gera destino ate origem. Invertemos para poder caminhar adiante.
        pathNodes.Reverse();
    }

    // Inicia o movimento do jogador pelo caminho calculado pelo Dijkstra.
    public void MovePlayerAlongPath()
    {
        if (pathNodes.Count == 0) return;

        // Se nenhum jogador foi informado, o objeto com este script sera movimentado.
        if (player == null)
        {
            player = transform;
        }

        if (playerMovement != null)
        {
            StopCoroutine(playerMovement);
        }
        playerMovement = StartCoroutine(MovePlayerOnPath());
    }

    // Move o jogador, no a no, ate o centro de cada celula do caminho.
    IEnumerator MovePlayerOnPath()
    {
        foreach (Node node in pathNodes)
        {
            Vector3 destination = NodeToWorld(node.x, node.y);
            // Mantem a altura atual do jogador, pois a grade usa Y igual a zero.
            destination.y = player.position.y;

            while (Vector3.Distance(player.position, destination) > 0.01f)
            {
                player.position = Vector3.MoveTowards(
                    player.position,
                    destination,
                    playerMoveSpeed * Time.deltaTime);
                yield return null;
            }

            // Garante que o jogador termine exatamente no centro da celula.
            player.position = destination;
        }

        playerMovement = null;
    }

    Node WorldToNode(Vector3 world)
    {
        // A grade esta fixa na origem global, como no AIMove.
        // Floor identifica a celula; Clamp evita indices fora da matriz.
        int x = Mathf.Clamp(Mathf.FloorToInt(world.x / nodeSize), 0, size.x - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(world.z / nodeSize), 0, size.y - 1);
        return nodes[x, y];
    }

    Vector3 NodeToWorld(int x, int y)
    {
        // Converte uma celula para uma posicao fixa no mundo.
        return new Vector3(x * nodeSize, 0, y * nodeSize);
    }

    bool IsWalkable(Vector3 center)
    {
        // A caixa e ligeiramente menor que o no para nao tocar obstaculos vizinhos.
        Vector3 halfExtents = new Vector3(nodeSize * .45f, obstacleHeight * .5f, nodeSize * .45f);
        // CheckBox retorna true quando encontra um Collider nas camadas bloqueadoras.
        return !Physics.CheckBox(center, halfExtents, Quaternion.identity, obstacleMask);
    }

    void OnDrawGizmos()
    {
        // Tambem desenha fora do Play Mode para facilitar a configuracao da grade.
        if (!showGridGizmos || size.x <= 0 || size.y <= 0) return;
        for (int x = 0; x < size.x; x++)
        for (int y = 0; y < size.y; y++)
        {
            // Fora da execucao, consulta diretamente o obstaculo; durante ela, usa o no.
            Node node = null;
            if (nodes != null)
            {
                node = nodes[x, y];
            }

            bool walkable;
            if (node == null)
            {
                walkable = IsWalkable(NodeToWorld(x, y));
            }
            else
            {
                walkable = node.cost != int.MaxValue;
            }
            Gizmos.color = GetColor(node, walkable);
            Gizmos.DrawCube(NodeToWorld(x, y), new Vector3(nodeSize * .9f, .05f, nodeSize * .9f));
        }
    }

    Color GetColor(Node node, bool walkable)
    {
        // Vermelho: obstaculo; verde: ainda nao visitado.
        if (!walkable) return new Color(1, 0, 0, .35f);
        if (node == null || node.state == State.None) return new Color(0, 1, 0, .25f);
        // Laranja: lista aberta; azul: no ja processado; ciano: rota final.
        if (node.state == State.Open) return new Color(1, .65f, 0, .55f);
        if (node.state == State.Closed) return new Color(.2f, .5f, 1, .55f);
        return Color.cyan;
    }
}
