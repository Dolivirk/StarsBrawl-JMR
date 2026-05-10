using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using static UnityEngine.ParticleSystem;
using Unity.VisualScripting;

/// <summary>
/// Controla la pantalla de detalles del brawler. Lee el brawler seleccionado desde la sesión del usuario,
/// obtiene sus datos mediante SQL y rellena todos los campos de la interfaz de usuario.
/// </summary>
[DisallowMultipleComponent]
public sealed class BrawlerDetailsController : MonoBehaviour
{
    private const string BrawlersScene    = "Brawlers";
    private const float  LevelScaleFactor = 0.10f;

    [Header("Dependencies")]
    [SerializeField] private SQL sql;

    /// <summary>
    /// Se utiliza un brawler_id de reserva solo cuando se accede al Modo de juego directamente en esta escena.
    /// y la sesión de usuario no contiene ningún brawler (por ejemplo, durante las pruebas de escenas aisladas)
    /// No tiene efecto al acceder mediante la navegación normal Brawlers → BrawlerDetails.
    /// </summary>
    [Header("Debug")]
    [SerializeField] [Min(1)] private int debugBrawlerId = 1;

    [Header("Brawler Info")]
    [SerializeField] private TextMeshProUGUI brawlerNameText;
    [SerializeField] private TextMeshProUGUI brawlerClassText;
    [SerializeField] private TextMeshProUGUI brawlerRarityText;

    [Header("Trophies")]
    [SerializeField] private TextMeshProUGUI trophiesText;

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI attributeText;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI superNameText;

    private void Start()
    {
        // Registre el esquema completo de la base de datos para que se puedan verificar todos los nombres de tablas y columnas.
        sql.LogAllTables();

        if (UserSession.Instance == null)
        {
            Debug.LogError("[BrawlerDetailsController] No user session found.");
            return;
        }

        string userId    = UserSession.Instance.UserId;
        int    brawlerId = UserSession.Instance.BrawlerId;

        // Al entrar directamente a esta escena (p. ej., en el Editor sin pasar por Iniciar sesión
        // y Brawlers), se recurre al debugBrawlerId asignado por el inspector para que la pantalla
        // pueda probarse de forma aislada.
        if (brawlerId <= 0)
        {
            Debug.LogWarning($"[BrawlerDetailsController] No brawler_id in session — using debugBrawlerId={debugBrawlerId}.");
            brawlerId = debugBrawlerId;
        }

        // El ID de usuario está vacío al entrar directamente a esta escena (depuración/pruebas aisladas).
        // TryGetBrawlerDetails maneja un ID de usuario nulo omitiendo el filtro user_id,
        // por lo que solo se produce un fallo grave aquí al llegar a través del flujo normal sin un usuario configurado.
        if (string.IsNullOrEmpty(userId) && UserSession.Instance.BrawlerId > 0)
        {
            Debug.LogError("[BrawlerDetailsController] UserId is empty. Enter the scene via Login → Brawlers, or set a valid userId in the session.");
            return;
        }

        if (!sql.TryGetBrawlerDetails(userId, brawlerId, out BrawlerDetailsData data))
        {
            Debug.LogWarning($"[BrawlerDetailsController] No data found for brawler_id {brawlerId}.");
            return;
        }

        PopulateUI(data);
    }

    /// <summary>Rellena todos los elementos de la interfaz de usuario con los datos del brawler obtenidos.</summary>
    private void PopulateUI(BrawlerDetailsData data)
    {
        brawlerNameText.text   = data.Name;
        brawlerClassText.text  = data.ClassName;
        brawlerRarityText.text = data.RarityName;

        trophiesText.text = data.Trophies.ToString();

        descriptionText.text = data.Description;

        // Ocultar el campo de atributo cuando no haya descripción disponible.
        bool hasAttribute = !string.IsNullOrWhiteSpace(data.AttributeDescription);
        attributeText.gameObject.SetActive(hasAttribute);
        if (hasAttribute)
            attributeText.text = data.AttributeDescription;

        levelText.text     = data.Level.ToString();
        superNameText.text = string.IsNullOrWhiteSpace(data.SuperName) ? "-" : data.SuperName;

        // Aumenta la salud y el daño en un 10% por cada nivel por encima de 1.
        float multiplier = 1f + (data.Level - 1) * LevelScaleFactor;
        int   health     = Mathf.RoundToInt(data.BaseHealth * multiplier);
        int   damage     = Mathf.RoundToInt(data.BaseDamage * multiplier);

        healthText.text = health.ToString();

        // Construye la cadena de ataque:
        // - Muestra "daño x proyectiles" cuando hay varios proyectiles por disparo
        // - Añade "x ataques disparos" cuando hay varios disparos por activación.
        string attackDisplay = data.ProjectilesPerAttack > 1
            ? $"{damage} x{data.ProjectilesPerAttack}"
            : damage.ToString();

        if (data.Attacks > 1)
            attackDisplay += $" ({data.Attacks} disparos)";

        attackText.text = attackDisplay;
    }

    public void OnGoBackClicked()
    {
        SceneManager.LoadScene(BrawlersScene);
    }
}
