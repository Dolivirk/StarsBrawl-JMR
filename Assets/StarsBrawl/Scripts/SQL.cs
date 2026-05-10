using System;
using System.Collections.Generic;
using UnityEngine;
using MySqlConnector;
using System.Security.Cryptography;
using System.Drawing;

public class SQL : MonoBehaviour
{
    private const string Server     = "127.0.0.1";
    private const string Database   = "brawl_stars";
    private const string DbUser     = "root";
    private const string DbPassword = "root";

    private string ConnectionString =>
        $"Server={Server}; Database={Database}; User ID={DbUser}; Password={DbPassword}; SslMode=None;";

    /// <summary>Devuelve verdadero si existe una fila con el user_id dado en la tabla de usuarios.</summary>
    public bool UserExists(string userId)
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            const string query = "SELECT COUNT(*) FROM users WHERE user_id = @userId";
            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }
    }

    /// <summary>
    /// Obtiene el apodo, el bling, las monedas, las gemas y el total de trofeos del usuario especificado.
    /// El total de trofeos es la suma de todos los trofeos de los brawlers del grupo user_brawlers al que se unió el usuario user_id.
    /// </summary>
    public bool TryGetUserProfile(string userId, out UserProfile profile)
    {
        profile = default;

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            const string query =
                "SELECT u.nickname, u.bling, u.coins, u.gems, " +
                "COALESCE(SUM(ub.trophies), 0) AS total_trophies " +
                "FROM users u " +
                "LEFT JOIN user_brawlers ub ON u.user_id = ub.user_id " +
                "WHERE u.user_id = @userId " +
                "GROUP BY u.user_id, u.nickname, u.bling, u.coins, u.gems";

            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return false;

                    profile = new UserProfile
                    {
                        Nickname = reader.GetString("nickname"),
                        Trophies = reader.GetInt32("total_trophies"),
                        Bling    = reader.GetInt32("bling"),
                        Coins    = reader.GetInt32("coins"),
                        Gems     = reader.GetInt32("gems"),
                    };
                    return true;
                }
            }
        }
    }

    /// <summary>
    /// Obtiene los trofeos y el nivel del brawler cuyo user_brawlers.user_id
    ///coincide con el user_id dado mediante una unión interna(INNER JOIN) con la tabla de usuarios.
    /// Devuelve la fila con el mayor número de trofeos cuando el usuario posee varios brawlers.
    /// </summary>
    public bool TryGetLastBrawlerStats(string userId, out BrawlerStats stats)
    {
        stats = default;

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            const string query =
                "SELECT ub.trophies, ub.level " +
                "FROM user_brawlers ub " +
                "INNER JOIN users u ON u.user_id = ub.user_id " +
                "WHERE ub.user_id = @userId " +
                "ORDER BY ub.trophies DESC " +
                "LIMIT 1";

            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return false;

                    stats = new BrawlerStats
                    {
                        Trophies = reader.GetInt32("trophies"),
                        Level    = reader.GetInt32("level"),
                    };
                    return true;
                }
            }
        }
    }

    /// <summary>
    /// Devuelve el número total de brawlers registrados en la tabla de brawlers.
    /// </summary>
    public int GetTotalBrawlerCount()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            const string query = "SELECT COUNT(*) FROM brawlers";
            using (var command = new MySqlCommand(query, connection))
            {
                return (int)(long)command.ExecuteScalar();
            }
        }
    }

    /// <summary>
    /// Obtiene los detalles completos del usuario para la pantalla Detalles del usuario.
    /// Funciona para cualquier user_id, tanto para el perfil del usuario que ha iniciado sesión (desde el menú principal)
    /// como para el perfil de cualquier otro jugador (desde Detalles de la partida).
    /// Utiliza BuildSchema + uniones match_user en todo momento, siguiendo el mismo patrón
    /// que utilizan GetRecentMatches y GetMatchParticipants.
    /// </summary>
    public bool TryGetUserDetails(string userId, out UserDetails details)
    {
        details = default;

        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();

                // Build a full schema map once — reused by every sub-query.
                var schema = BuildSchema(connection);

                // ── Column resolution ─────────────────────────────────────────────

                // users table
                var userCols    = schema.ContainsKey("users")        ? schema["users"]        : new HashSet<string>();
                var muCols      = schema.ContainsKey("match_user")   ? schema["match_user"]   : new HashSet<string>();
                var mCols       = schema.ContainsKey("matches")      ? schema["matches"]      : new HashSet<string>();
                var mtCols      = schema.ContainsKey("match_types")  ? schema["match_types"]  : new HashSet<string>();
                var ubCols      = schema.ContainsKey("user_brawlers")? schema["user_brawlers"]: new HashSet<string>();
                var bCols       = schema.ContainsKey("brawlers")     ? schema["brawlers"]     : new HashSet<string>();

                // users
                string uNicknameCol = ResolveName(userCols, "nickname", "name", "username");
                string uRankCol     = ResolveName(userCols, "rank_id", "rank", "rankId");

                // match_user
                string muUserCol    = ResolveName(muCols, "user_id",   "userId");
                string muMatchCol   = ResolveName(muCols, "match_id",  "matchId");
                string muResultCol  = ResolveName(muCols, "result",    "match_result", "outcome");
                string muBrawlCol   = ResolveName(muCols, "brawler_id","brawlerId");
                string muModalityCol= ResolveName(muCols, "modality",  "match_modality");

                // matches (for date ordering of streak)
                string mMatchCol    = ResolveName(mCols, "match_id",   "matchId");
                string mDateCol     = ResolveName(mCols, "match_date", "date", "created_at", "timestamp");
                string mTypeIdCol   = ResolveName(mCols, "match_type_id","match_types_id","type_id","game_mode_id");
                string mTypeStrCol  = ResolveName(mCols, "match_type", "match_types", "game_mode"); // plain string fallback
                string mModalityCol = ResolveName(mCols, "modality", "match_modality");

                // match_types lookup
                string mtIdCol   = ResolveName(mtCols, "match_type_id", "type_id", "id");
                string mtNameCol = ResolveName(mtCols, "name", "type_name", "match_type", "match_types");

                // Decide whether game mode is a FK (join match_types) or a plain string.
                bool modeIsFk = mTypeIdCol != null && mtIdCol != null && mtNameCol != null;

                // Effective expressions for mode and modality used in all sub-queries.
                string modeExpr     = modeIsFk
                    ? $"mt.{mtNameCol}"
                    : (mTypeStrCol != null ? $"m.{mTypeStrCol}" : "NULL");

                // Prefer match_user modality; fall back to matches modality with correct table prefix.
                string modalityExpr = muModalityCol != null
                    ? $"mu.{muModalityCol}"
                    : (mModalityCol != null ? $"m.{mModalityCol}" : "NULL");

                string resultExpr   = muResultCol != null ? $"mu.{muResultCol}" : "NULL";

                // JOIN snippets reused across sub-queries.
                string joinMatches     = mMatchCol != null && muMatchCol != null
                    ? $"INNER JOIN matches m ON m.{mMatchCol} = mu.{muMatchCol} "
                    : string.Empty;
                string joinMatchTypes  = modeIsFk
                    ? $"LEFT JOIN match_types mt ON mt.{mtIdCol} = m.{mTypeIdCol} "
                    : string.Empty;

                // user_brawlers
                string ubUserCol  = ResolveName(ubCols, "user_id",    "userId");
                string ubBrawlCol = ResolveName(ubCols, "brawler_id", "brawlerId");
                string ubTrophCol = ResolveName(ubCols, "trophies",   "trophy_count");

                // brawlers
                string bBrawlCol  = ResolveName(bCols, "brawler_id", "brawlerId");
                string bNameCol   = ResolveName(bCols, "name",       "brawler_name");

                // ── 1. Base profile: nickname, rank, total trophies ──────────────
                string rankSelect = uRankCol != null ? $", u.{uRankCol} AS rank" : string.Empty;
                string rankGroup  = uRankCol != null ? $", u.{uRankCol}"         : string.Empty;
                string trophyJoin = ubUserCol != null
                    ? $"LEFT JOIN user_brawlers ub ON ub.{ubUserCol} = u.user_id "
                    : string.Empty;
                string trophySelect = ubTrophCol != null
                    ? $"COALESCE(SUM(ub.{ubTrophCol}), 0)"
                    : "0";

                string profileQuery =
                    $"SELECT u.user_id, u.{uNicknameCol} AS nickname{rankSelect}, " +
                    $"{trophySelect} AS total_trophies " +
                    $"FROM users u {trophyJoin}" +
                    "WHERE u.user_id = @userId " +
                    $"GROUP BY u.user_id, u.{uNicknameCol}{rankGroup}";

                using (var cmd = new MySqlCommand(profileQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Debug.LogError($"[SQL] TryGetUserDetails: no user found for user_id='{userId}'.");
                            return false;
                        }

                        details.UserId   = reader.GetString("user_id");
                        details.Nickname = reader.GetString("nickname");
                        details.Trophies = reader.GetInt32("total_trophies");

                        if (uRankCol != null && !reader.IsDBNull(reader.GetOrdinal("rank")))
                            details.Rank = reader.GetInt32("rank");
                    }
                }

                // ── 2. Rank name — look up by trophies range in the ranks table ──
                details.RankName = QueryRankNameByTrophies(connection, schema, details.Trophies)
                                ?? QueryNameByForeignKey(connection, schema, "rank_id", details.Rank)
                                ?? QueryNameByForeignKey(connection, schema, "rank",    details.Rank)
                                ?? (details.Rank > 0 ? details.Rank.ToString() : "-");

                // Guard: if match_user is unavailable, skip the match-based stats.
                if (muUserCol == null || string.IsNullOrEmpty(joinMatches))
                {
                    Debug.LogWarning("[SQL] TryGetUserDetails: match_user table or join columns missing — match stats will be zero.");
                    details.MostPlayedBrawler = "-";
                    details.BestBrawler       = "-";
                    details.MostPlayedMode    = "-";
                    return true;
                }

                // ── Descubre los valores reales de la base de datos para el resultado, la modalidad y el modo ─────
                // En lugar de adivinar palabras clave de cadena, recuperamos todos los valores distintos
                // y elegimos la mejor coincidencia utilizando una lista de candidatos clasificados.
                Debug.Log($"[SQL] match_user columns: {string.Join(", ", muCols)}");
                Debug.Log($"[SQL] matches columns: {string.Join(", ", mCols)}");
                Debug.Log($"[SQL] Expressions — result:'{resultExpr}' mode:'{modeExpr}' modality:'{modalityExpr}'");

                LogDistinctValues(connection, muUserCol, resultExpr,   joinMatches, joinMatchTypes, "result");
                LogDistinctValues(connection, muUserCol, modeExpr,     joinMatches, joinMatchTypes, "mode");
                LogDistinctValues(connection, muUserCol, modalityExpr, joinMatches, joinMatchTypes, "modality");

                // Win value: try known keywords first, then fall back to the most-frequent result value.
                string winValue = ResolveDistinctValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes, "win")
                               ?? ResolveDistinctValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes, "victoria")
                               ?? ResolveDistinctValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes, "ganó")
                               ?? ResolveDistinctValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes, "gano")
                               ?? ResolveDistinctValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes, "ganador")
                               ?? ResolveDistinctValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes, "1")
                               ?? ResolveMostFrequentValue(connection, muUserCol, resultExpr, joinMatches, joinMatchTypes);

                // Showdown mode: try known keywords, then most-frequent mode value.
                string showdownValue = ResolveDistinctValue(connection, muUserCol, modeExpr, joinMatches, joinMatchTypes, "showdown")
                                    ?? ResolveDistinctValue(connection, muUserCol, modeExpr, joinMatches, joinMatchTypes, "supervivencia")
                                    ?? ResolveDistinctValue(connection, muUserCol, modeExpr, joinMatches, joinMatchTypes, "solo")
                                    ?? ResolveDistinctValue(connection, muUserCol, modeExpr, joinMatches, joinMatchTypes, "duo")
                                    ?? ResolveDistinctValue(connection, muUserCol, modeExpr, joinMatches, joinMatchTypes, "battle");

                // 3v3 modality: try known keywords, then most-frequent modality value.
                string tripleValue = ResolveDistinctValue(connection, muUserCol, modalityExpr, joinMatches, joinMatchTypes, "3v3")
                                  ?? ResolveDistinctValue(connection, muUserCol, modalityExpr, joinMatches, joinMatchTypes, "trio")
                                  ?? ResolveDistinctValue(connection, muUserCol, modalityExpr, joinMatches, joinMatchTypes, "equipo")
                                  ?? ResolveDistinctValue(connection, muUserCol, modalityExpr, joinMatches, joinMatchTypes, "team")
                                  ?? ResolveDistinctValue(connection, muUserCol, modalityExpr, joinMatches, joinMatchTypes, "triple");

                Debug.Log($"[SQL] Resolved DB values — win='{winValue}' showdown='{showdownValue}' 3v3='{tripleValue}'");

                // ── 3. Showdown wins ─────────────────────────────────────────────
                if (showdownValue != null && winValue != null && modeExpr != "NULL")
                {
                    string showdownQuery =
                        $"SELECT COUNT(*) " +
                        $"FROM match_user mu " +
                        joinMatches +
                        joinMatchTypes +
                        $"WHERE mu.{muUserCol} = @userId " +
                        $"  AND {modeExpr} = @showdownVal " +
                        $"  AND {resultExpr} = @winVal";

                    Debug.Log($"[SQL] ShowdownWins query: {showdownQuery}");
                    using (var cmd = new MySqlCommand(showdownQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId",      userId);
                        cmd.Parameters.AddWithValue("@showdownVal", showdownValue);
                        cmd.Parameters.AddWithValue("@winVal",      winValue);
                        details.ShowdownWins = (int)(long)cmd.ExecuteScalar();
                    }
                }
                else if (winValue != null && resultExpr != "NULL")
                {
                    // Mode column unavailable — fall back to total wins across all modes.
                    string totalWinsQuery =
                        $"SELECT COUNT(*) FROM match_user mu {joinMatches}" +
                        $"WHERE mu.{muUserCol} = @userId AND {resultExpr} = @winVal";

                    Debug.Log($"[SQL] ShowdownWins fallback (total wins) query: {totalWinsQuery}");
                    using (var cmd = new MySqlCommand(totalWinsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@winVal", winValue);
                        details.ShowdownWins = (int)(long)cmd.ExecuteScalar();
                    }
                }
                else
                {
                    Debug.Log("[SQL] Showdown wins: could not resolve mode or win value — defaulting to 0.");
                }

                // ── 4. 3v3 wins ──────────────────────────────────────────────────
                Debug.Log($"[SQL] 3v3 wins check — tripleValue='{tripleValue}' winValue='{winValue}' modalityExpr='{modalityExpr}'");

                if (tripleValue != null && winValue != null && modalityExpr != "NULL")
                {
                    string tripleQuery =
                        $"SELECT COUNT(*) " +
                        $"FROM match_user mu " +
                        joinMatches +
                        joinMatchTypes +
                        $"WHERE mu.{muUserCol} = @userId " +
                        $"  AND {modalityExpr} = @tripleVal " +
                        $"  AND {resultExpr} = @winVal";

                    Debug.Log($"[SQL] TripleWins query: {tripleQuery}");
                    using (var cmd = new MySqlCommand(tripleQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId",    userId);
                        cmd.Parameters.AddWithValue("@tripleVal", tripleValue);
                        cmd.Parameters.AddWithValue("@winVal",    winValue);
                        details.TripleWins = (int)(long)cmd.ExecuteScalar();
                    }
                }
                else if (winValue != null && resultExpr != "NULL" && modalityExpr == "NULL")
                {
                    // No modality column found — fall back to total wins across all modes.
                    Debug.Log("[SQL] 3v3 wins: no modality column found — falling back to total wins.");
                    string totalWinsQuery =
                        $"SELECT COUNT(*) FROM match_user mu {joinMatches}" +
                        $"WHERE mu.{muUserCol} = @userId AND {resultExpr} = @winVal";

                    using (var cmd = new MySqlCommand(totalWinsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@winVal", winValue);
                        details.TripleWins = (int)(long)cmd.ExecuteScalar();
                    }
                }
                else
                {
                    string reason = tripleValue == null ? "tripleValue is null (no matching keyword found in modality column)"
                                  : winValue    == null ? "winValue is null (no matching keyword found in result column)"
                                  : $"modalityExpr='{modalityExpr}' (no modality column detected)";
                    Debug.Log($"[SQL] 3v3 wins: could not resolve — {reason}. Defaulting to 0.");
                }

                // ── 5. Current win streak ────────────────────────────────────────
                // Fetch results ordered by match date descending and count consecutive wins.
                string orderBy = mDateCol != null
                    ? $"m.{mDateCol} DESC, m.{mMatchCol} DESC"
                    : $"m.{mMatchCol} DESC";

                string streakQuery =
                    $"SELECT {resultExpr} AS result " +
                    $"FROM match_user mu " +
                    joinMatches +
                    $"WHERE mu.{muUserCol} = @userId " +
                    $"ORDER BY {orderBy}";

                using (var cmd = new MySqlCommand(streakQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    int streak = 0;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(reader.GetOrdinal("result")))
                                break;
                            string r = reader.GetString("result");
                            // Compare against the discovered win value; fall back to "win" if unresolved.
                            string winCmp = winValue ?? "win";
                            if (r.Equals(winCmp, System.StringComparison.OrdinalIgnoreCase))
                                streak++;
                            else
                                break;
                        }
                    }
                    details.WinStreak = streak;
                }

                // ── 6. Most-played brawler ───────────────────────────────────────
                // Requires brawler_id on match_user and a brawlers table with a name column.
                if (muBrawlCol != null && bBrawlCol != null && bNameCol != null)
                {
                    string mostPlayedQuery =
                        $"SELECT b.{bNameCol} " +
                        $"FROM match_user mu " +
                        $"INNER JOIN brawlers b ON b.{bBrawlCol} = mu.{muBrawlCol} " +
                        $"WHERE mu.{muUserCol} = @userId " +
                        $"GROUP BY mu.{muBrawlCol}, b.{bNameCol} " +
                        "ORDER BY COUNT(*) DESC " +
                        "LIMIT 1";

                    using (var cmd = new MySqlCommand(mostPlayedQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var r = cmd.ExecuteScalar();
                        details.MostPlayedBrawler = r != null ? r.ToString() : "-";
                    }

                    // ── 7. Best brawler (highest win rate, min 1 match) ──────────
                    string winCaseVal = winValue ?? "Win";
                    string bestBrawlerQuery =
                        $"SELECT b.{bNameCol}, " +
                        $"SUM(CASE WHEN {resultExpr} = '{winCaseVal}' THEN 1 ELSE 0 END) / COUNT(*) AS win_rate " +
                        $"FROM match_user mu " +
                        joinMatches +
                        $"INNER JOIN brawlers b ON b.{bBrawlCol} = mu.{muBrawlCol} " +
                        $"WHERE mu.{muUserCol} = @userId " +
                        $"GROUP BY mu.{muBrawlCol}, b.{bNameCol} " +
                        "HAVING COUNT(*) >= 1 " +
                        "ORDER BY win_rate DESC " +
                        "LIMIT 1";

                    using (var cmd = new MySqlCommand(bestBrawlerQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var r = cmd.ExecuteScalar();
                        details.BestBrawler = r != null ? r.ToString() : "-";
                    }
                }
                else
                {
                    details.MostPlayedBrawler = "-";
                    details.BestBrawler       = "-";
                }

                // ── 8. Most-played game mode ─────────────────────────────────────
                if (modeExpr != "NULL")
                {
                    string mostPlayedModeQuery =
                        $"SELECT {modeExpr} AS mode_name " +
                        $"FROM match_user mu " +
                        joinMatches +
                        joinMatchTypes +
                        $"WHERE mu.{muUserCol} = @userId " +
                        $"GROUP BY {modeExpr} " +
                        "ORDER BY COUNT(*) DESC " +
                        "LIMIT 1";

                    using (var cmd = new MySqlCommand(mostPlayedModeQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var r = cmd.ExecuteScalar();
                        details.MostPlayedMode = r != null ? r.ToString() : "-";
                    }
                }
                else
                {
                    details.MostPlayedMode = "-";
                }

                return true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SQL] TryGetUserDetails exception: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Registra todas las tablas de la base de datos y los nombres de todas sus columnas.
    /// Llama a esta función una sola vez para mapear el esquema completo cuando se desconocen los nombres de las columnas.
    /// </summary>
    public void LogAllTables()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            var tableNames = new List<string>();
            using (var cmd = new MySqlCommand("SHOW TABLES", connection))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    tableNames.Add(reader.GetString(0));

            foreach (string table in tableNames)
            {
                var sb = new System.Text.StringBuilder($"[SQL] Table '{table}': ");
                using (var cmd = new MySqlCommand($"DESCRIBE `{table}`", connection))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        sb.Append(reader.GetString("Field")).Append(", ");
                Debug.Log(sb.ToString());
            }
        }
    }

    /// <summary>
    /// Obtiene todos los datos necesarios para la pantalla BrawlerDetails de un usuario y brawler específicos.
    /// Orden de consulta: primero user_brawlers (confirma la propiedad y resuelve el brawler_id real
    /// para este usuario), luego brawlers para las estadísticas base, y finalmente búsquedas de claves foráneas para los nombres de clase y rareza.
    /// Utiliza BuildSchema + ResolveName en todo el proceso, por lo que las variaciones en los nombres de las columnas se gestionan automáticamente.
    /// </summary>
    public bool TryGetBrawlerDetails(string userId, int brawlerId, out BrawlerDetailsData data)
    {
        data = default;

        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();

                // Build a full schema map once — reused by every sub-query.
                var schema = BuildSchema(connection);

                // ── Resolve user_brawlers columns ────────────────────────────────
                if (!schema.ContainsKey("user_brawlers"))
                {
                    Debug.LogError("[SQL] TryGetBrawlerDetails: user_brawlers table not found in schema.");
                    return false;
                }

                var ubCols = schema["user_brawlers"];

                string ubUserCol  = ResolveName(ubCols, "user_id",   "userId");
                string ubBrawlCol = ResolveName(ubCols, "brawler_id","brawlerId");
                string ubTrophCol = ResolveName(ubCols, "trophies",  "trophy_count");
                string ubLevelCol = ResolveName(ubCols, "level",     "brawler_level");

                if (ubUserCol == null || ubBrawlCol == null)
                {
                    Debug.LogError("[SQL] TryGetBrawlerDetails: could not resolve user_id or brawler_id " +
                                   "column in user_brawlers. Run LogAllTables().");
                    return false;
                }

                // ── 1. Confirm ownership: find the user_brawlers row for this user ──
                // Esta es la fuente autorizada de trofeos, nivel y brawler_id confirmado.
                // Cuando userId está vacío (depuración / pruebas en escenas aisladas), el filtro user_id se
                // omite para que la fila del brawler pueda encontrarse sin una sesión iniciada.
                var ubSelectParts = new System.Text.StringBuilder(
                    $"SELECT {ubBrawlCol} AS brawler_id");

                if (ubTrophCol != null) ubSelectParts.Append($", {ubTrophCol} AS trophies");
                if (ubLevelCol != null) ubSelectParts.Append($", {ubLevelCol} AS level");

                bool hasUserId = !string.IsNullOrEmpty(userId);

                string ubQuery = hasUserId
                    ? $"{ubSelectParts} FROM user_brawlers WHERE {ubUserCol} = @userId AND {ubBrawlCol} = @brawlerId LIMIT 1"
                    : $"{ubSelectParts} FROM user_brawlers WHERE {ubBrawlCol} = @brawlerId LIMIT 1";

                Debug.Log($"[SQL] TryGetBrawlerDetails user_brawlers query: {ubQuery}");

                int confirmedBrawlerId = brawlerId;

                using (var cmd = new MySqlCommand(ubQuery, connection))
                {
                    if (hasUserId)
                        cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@brawlerId", brawlerId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Debug.LogError($"[SQL] TryGetBrawlerDetails: no user_brawlers row for " +
                                           $"user_id='{userId}' brawler_id={brawlerId}. " +
                                           "The brawler may not be unlocked by this user.");
                            return false;
                        }

                        confirmedBrawlerId = reader.GetInt32("brawler_id");

                        data.Trophies = ubTrophCol != null && !reader.IsDBNull(reader.GetOrdinal("trophies"))
                            ? reader.GetInt32("trophies") : 0;

                        data.Level = ubLevelCol != null && !reader.IsDBNull(reader.GetOrdinal("level"))
                            ? reader.GetInt32("level") : 1;
                    }
                }

                Debug.Log($"[SQL] TryGetBrawlerDetails: confirmed brawler_id={confirmedBrawlerId} " +
                          $"trophies={data.Trophies} level={data.Level}");

                // ── Resolve brawlers columns ─────────────────────────────────────
                // Schema: brawler_id, name, description, health, movement_speed,
                //         class_id, rarity_id, attack_id, super_id, trait_id
                var bCols = schema.ContainsKey("brawlers") ? schema["brawlers"] : new HashSet<string>();

                string bBrawlerIdCol = ResolveName(bCols, "brawler_id", "brawlerId", "id");
                string bNameCol      = ResolveName(bCols, "name", "brawler_name", "nombre");
                string bDescCol      = ResolveName(bCols, "description", "brawler_description", "descripcion");
                string bHealthCol    = ResolveName(bCols, "health", "base_health", "hp", "vida", "salud");
                string bClassIdCol   = ResolveName(bCols, "class_id", "clase_id", "classId");
                string bRarityIdCol  = ResolveName(bCols, "rarity_id", "rareza_id", "rarityId");
                // FK references to other tables — damage/super come from these.
                string bAttackIdCol  = ResolveName(bCols, "attack_id", "attackId");
                string bSuperIdCol   = ResolveName(bCols, "super_id",  "superId");

                if (bBrawlerIdCol == null || bNameCol == null || bHealthCol == null)
                {
                    Debug.LogError($"[SQL] TryGetBrawlerDetails: could not resolve required brawlers columns. " +
                                   $"Resolved — brawler_id:'{bBrawlerIdCol}' name:'{bNameCol}' health:'{bHealthCol}'. " +
                                   $"Actual brawlers columns: {string.Join(", ", bCols)}");
                    return false;
                }

                // ── 2. Brawler base data ─────────────────────────────────────────
                var brawlerSelect = new System.Text.StringBuilder(
                    $"SELECT {bNameCol} AS name, {bDescCol ?? "''"} AS description, {bHealthCol} AS base_health");

                if (bClassIdCol  != null) brawlerSelect.Append($", {bClassIdCol} AS class_id");
                if (bRarityIdCol != null) brawlerSelect.Append($", {bRarityIdCol} AS rarity_id");
                if (bAttackIdCol != null) brawlerSelect.Append($", {bAttackIdCol} AS attack_id");
                if (bSuperIdCol  != null) brawlerSelect.Append($", {bSuperIdCol} AS super_id");
                brawlerSelect.Append($" FROM brawlers WHERE {bBrawlerIdCol} = @confirmedBrawlerId");

                string brawlerQuery = brawlerSelect.ToString();
                Debug.Log($"[SQL] TryGetBrawlerDetails brawler query: {brawlerQuery}");

                int classId = 0, rarityId = 0, attackId = 0, superId = 0;

                using (var cmd = new MySqlCommand(brawlerQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@confirmedBrawlerId", confirmedBrawlerId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Debug.LogError($"[SQL] TryGetBrawlerDetails: no row in brawlers for brawler_id={confirmedBrawlerId}.");
                            return false;
                        }

                        data.Name       = reader.GetString("name");
                        data.Description = !reader.IsDBNull(reader.GetOrdinal("description"))
                            ? reader.GetString("description") : string.Empty;
                        data.BaseHealth = reader.GetInt32("base_health");

                        if (bClassIdCol  != null && !reader.IsDBNull(reader.GetOrdinal("class_id")))
                            classId  = reader.GetInt32("class_id");
                        if (bRarityIdCol != null && !reader.IsDBNull(reader.GetOrdinal("rarity_id")))
                            rarityId = reader.GetInt32("rarity_id");
                        if (bAttackIdCol != null && !reader.IsDBNull(reader.GetOrdinal("attack_id")))
                            attackId = reader.GetInt32("attack_id");
                        if (bSuperIdCol  != null && !reader.IsDBNull(reader.GetOrdinal("super_id")))
                            superId  = reader.GetInt32("super_id");
                    }
                }

                // ── 3. Class and rarity names via FK lookup ──────────────────────
                data.ClassName  = QueryNameByForeignKey(connection, schema, "class_id",  classId)  ?? "-";
                data.RarityName = QueryNameByForeignKey(connection, schema, "rarity_id", rarityId) ?? "-";

                // ── 4. Attack data — looked up by attack_id PK, not brawler_id FK ─
                // brawlers.attack_id → attacks.attack_id (PK)
                data.BaseDamage           = 0;
                data.ProjectilesPerAttack = 1;
                data.Attacks              = 1;

                if (attackId > 0 && schema.ContainsKey("attacks"))
                {
                    var atkCols = schema["attacks"];
                    Debug.Log($"[SQL] attacks columns: {string.Join(", ", atkCols)}");

                    string atkPkCol          = ResolveName(atkCols, "attack_id",  "attackId", "id");
                    string atkDamageCol      = ResolveName(atkCols, "damage",     "base_damage", "atk_damage");
                    string atkProjectilesCol = ResolveName(atkCols, "projectiles_per_attack", "projectiles", "num_projectiles");
                    string atkAttacksCol     = ResolveName(atkCols, "num_attacks", "attacks", "attacks_per_use");

                    if (atkPkCol != null)
                    {
                        var atkSelect = new System.Text.StringBuilder("SELECT 1");
                        if (atkDamageCol      != null) atkSelect.Append($", {atkDamageCol} AS atk_damage");
                        if (atkProjectilesCol != null) atkSelect.Append($", {atkProjectilesCol} AS atk_projectiles");
                        if (atkAttacksCol     != null) atkSelect.Append($", {atkAttacksCol} AS atk_attacks");

                        string atkQuery = $"{atkSelect} FROM attacks WHERE {atkPkCol} = @attackId LIMIT 1";
                        Debug.Log($"[SQL] TryGetBrawlerDetails attacks query: {atkQuery}");

                        using (var cmd = new MySqlCommand(atkQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@attackId", attackId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    if (atkDamageCol != null && !reader.IsDBNull(reader.GetOrdinal("atk_damage")))
                                        data.BaseDamage = reader.GetInt32("atk_damage");
                                    if (atkProjectilesCol != null && !reader.IsDBNull(reader.GetOrdinal("atk_projectiles")))
                                        data.ProjectilesPerAttack = reader.GetInt32("atk_projectiles");
                                    if (atkAttacksCol != null && !reader.IsDBNull(reader.GetOrdinal("atk_attacks")))
                                        data.Attacks = reader.GetInt32("atk_attacks");
                                }
                                else
                                {
                                    Debug.LogWarning($"[SQL] TryGetBrawlerDetails: no attacks row for attack_id={attackId}.");
                                }
                            }
                        }
                    }
                }

                // ── 5. Super name — looked up by super_id PK ─────────────────────
                // brawlers.super_id → supers.super_id (PK)
                data.SuperName = string.Empty;

                if (superId > 0 && schema.ContainsKey("supers"))
                {
                    var supCols = schema["supers"];
                    Debug.Log($"[SQL] supers columns: {string.Join(", ", supCols)}");

                    string supPkCol   = ResolveName(supCols, "super_id",  "superId", "id");
                    string supNameCol = ResolveName(supCols, "name", "super_name", "nombre");

                    if (supPkCol != null && supNameCol != null)
                    {
                        string supQuery = $"SELECT {supNameCol} AS super_name FROM supers WHERE {supPkCol} = @superId LIMIT 1";
                        Debug.Log($"[SQL] TryGetBrawlerDetails supers query: {supQuery}");

                        using (var cmd = new MySqlCommand(supQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@superId", superId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read() && !reader.IsDBNull(reader.GetOrdinal("super_name")))
                                    data.SuperName = reader.GetString("super_name");
                                else
                                    Debug.LogWarning($"[SQL] TryGetBrawlerDetails: no supers row for super_id={superId}.");
                            }
                        }
                    }
                }

                Debug.Log($"[SQL] TryGetBrawlerDetails: name='{data.Name}' class='{data.ClassName}' " +
                          $"rarity='{data.RarityName}' damage={data.BaseDamage} " +
                          $"projectiles={data.ProjectilesPerAttack} attacks={data.Attacks} " +
                          $"super='{data.SuperName}'");

                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SQL] TryGetBrawlerDetails exception: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    // Devuelve el valor no nulo que aparece con mayor frecuencia para la expresión dada.
    // Se utiliza como último recurso cuando ningún candidato de palabra clave coincide.
    private string ResolveMostFrequentValue(
        MySqlConnection connection,
        string          userCol,
        string          valueExpr,
        string          joinMatches,
        string          joinMatchTypes)
    {
        if (valueExpr == "NULL" || string.IsNullOrEmpty(valueExpr)) return null;

        string query =
            $"SELECT {valueExpr} AS val, COUNT(*) AS cnt " +
            $"FROM match_user mu " +
            joinMatches +
            joinMatchTypes +
            $"WHERE mu.{userCol} IS NOT NULL AND {valueExpr} IS NOT NULL " +
            $"GROUP BY {valueExpr} ORDER BY cnt DESC LIMIT 1";

        try
        {
            using (var cmd = new MySqlCommand(query, connection))
            using (var reader = cmd.ExecuteReader())
                if (reader.Read() && !reader.IsDBNull(0))
                    return reader.GetString(0);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SQL] ResolveMostFrequentValue failed: {ex.Message}");
        }

        return null;
    }

    // Registra todos los valores DISTINTOS para una expresión dada, de modo que las discrepancias entre el nombre de la columna y el valor
    // sean visibles en la consola sin necesidad de un cliente de base de datos independiente.
    private void LogDistinctValues(
        MySqlConnection connection,
        string          userCol,
        string          valueExpr,
        string          joinMatches,
        string          joinMatchTypes,
        string          label)
    {
        if (valueExpr == "NULL" || string.IsNullOrEmpty(valueExpr)) { Debug.Log($"[SQL] distinct {label}: <expression is NULL>"); return; }

        string query =
            $"SELECT DISTINCT {valueExpr} AS val " +
            $"FROM match_user mu " +
            joinMatches +
            joinMatchTypes +
            $"WHERE mu.{userCol} IS NOT NULL";

        try
        {
            var values = new System.Collections.Generic.List<string>();
            using (var cmd = new MySqlCommand(query, connection))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    values.Add(reader.IsDBNull(0) ? "<null>" : reader.GetString(0));

            Debug.Log($"[SQL] distinct {label} values: [{string.Join(", ", values)}]");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SQL] LogDistinctValues('{label}') failed: {ex.Message}");
        }
    }

    // Busca valores DISTINTOS para la expresión dada y devuelve aquel cuya
    // forma en minúsculas contenga la palabra clave dada(p.ej., "win", "showdown", "3v3").
    // Devuelve null si la expresión es "NULL" o no se encuentra ningún valor coincidente.
    private string ResolveDistinctValue(
        MySqlConnection connection,
        string          userCol,
        string          valueExpr,
        string          joinMatches,
        string          joinMatchTypes,
        string          keyword)
    {
        if (valueExpr == "NULL" || string.IsNullOrEmpty(valueExpr))
            return null;

        string query =
            $"SELECT DISTINCT {valueExpr} AS val " +
            $"FROM match_user mu " +
            joinMatches +
            joinMatchTypes +
            $"WHERE mu.{userCol} IS NOT NULL AND {valueExpr} IS NOT NULL";

        try
        {
            using (var cmd = new MySqlCommand(query, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.IsDBNull(0)) continue;
                    string val = reader.GetString(0);
                    if (val.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        return val;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SQL] ResolveDistinctValue('{keyword}') failed: {ex.Message}");
        }

        return null;
    }

    // Busca el nombre del rango cuyo rango de trofeos contenga el total de trofeos especificado.
    // Escanea el esquema en busca de una tabla que contenga columnas para el número mínimo y máximo de trofeos, además de una columna para el nombre.
    private string QueryRankNameByTrophies(
        MySqlConnection connection,
        Dictionary<string, HashSet<string>> schema,
        int trophies)
    {
        foreach (var kv in schema)
        {
            string table = kv.Key;
            HashSet<string> cols = kv.Value;

            string minCol  = ResolveName(cols, "min_trophies", "trophies_min", "min_trophy", "trophies_from");
            string maxCol  = ResolveName(cols, "max_trophies", "trophies_max", "max_trophy", "trophies_to");
            string nameCol = ResolveName(cols, "name", "rank_name", "type_name");

            if (minCol == null || maxCol == null || nameCol == null)
                continue;

            string query = $"SELECT `{nameCol}` FROM `{table}` " +
                           $"WHERE @trophies BETWEEN `{minCol}` AND `{maxCol}` " +
                           "LIMIT 1";

            Debug.Log($"[SQL] QueryRankNameByTrophies query: {query} (trophies={trophies})");

            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@trophies", trophies);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return result.ToString();
            }
        }

        return null;
    }

    // Escanea todas las tablas del esquema para encontrar una cuya clave primaria coincida con el nombre de la columna de clave foránea dada,
    // luego devuelve el valor de su columna 'nombre' para el ID dado.
    private string QueryNameByForeignKey(
        MySqlConnection connection,
        Dictionary<string, HashSet<string>> schema,
        string foreignKeyCol,
        int id)
    {
        if (id <= 0) return null;

        foreach (var kv in schema)
        {
            string table = kv.Key;
            HashSet<string> cols = kv.Value;

            // Busque una tabla cuya clave primaria tenga el mismo nombre que la clave foránea (por ejemplo, class_id → classes).
            if (!cols.Contains(foreignKeyCol) || !cols.Contains("name"))
                continue;

            // Omitir las tablas de unión/usuario que también contienen esta clave foránea como clave externa.
            // Una tabla "primaria" normalmente tiene la clave foránea como su propia clave primaria.
            string query = $"SELECT name FROM `{table}` WHERE `{foreignKeyCol}` = @id LIMIT 1";
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                var result = cmd.ExecuteScalar();
                if (result != null)
                    return result.ToString();
            }
        }

        return null;
    }

    // Crea un mapa que relaciona el nombre de la tabla con el conjunto de nombres de las columnas de la base de datos actual.
    private Dictionary<string, HashSet<string>> BuildSchema(MySqlConnection connection)
    {
        var tableNames = new List<string>();
        using (var cmd = new MySqlCommand("SHOW TABLES", connection))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                tableNames.Add(reader.GetString(0));

        var schema = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (string table in tableNames)
        {
            var cols = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            using (var cmd = new MySqlCommand($"DESCRIBE `{table}`", connection))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    cols.Add(reader.GetString("Field"));
            schema[table] = cols;
        }

        return schema;
    }

    // Devuelve el primer candidato que existe en el conjunto de columnas dado, o null.
    private static string ResolveName(HashSet<string> cols, params string[] candidates)
    {
        foreach (string c in candidates)
            if (cols.Contains(c))
                return c;
        return null;
    }

    /// <summary>
    /// Registra todas las tablas y los nombres de todas sus columnas en la consola de Unity.
    /// Se llama una vez al inicio para confirmar el esquema completo de la base de datos cuando se desconocen los nombres de las columnas.
    /// </summary>
    public void LogMatchesSchema()
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();
            var schema = BuildSchema(connection);
            foreach (var kv in schema)
            {
                var sb = new System.Text.StringBuilder($"[SQL] Table '{kv.Key}': ");
                foreach (string col in kv.Value)
                    sb.Append(col).Append(", ");
                Debug.Log(sb.ToString());
            }
        }
    }

    /// <summary>
    /// Devuelve los últimos veinte partidos en los que participó el usuario que inició sesión,
    /// ordenados por fecha de partido de forma descendente. Combina match_user → matches → match_types
    /// utiliza la detección de esquema en tiempo de ejecución para que las variaciones en los nombres de las columnas se gestionen automáticamente.
    /// </summary>
    public List<MatchEntry> GetRecentMatches(string userId)
    {
        var results = new List<MatchEntry>();

        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();

                // Crea un mapa de esquema completo una sola vez; esto refleja el enfoque de TryGetBrawlerDetails.
                var schema = BuildSchema(connection);

                // ── match_user table ────────────────────────────────────────────────
                if (!schema.ContainsKey("match_user"))
                {
                    Debug.LogError("[SQL] GetRecentMatches: table 'match_user' not found. Run LogMatchesSchema().");
                    return results;
                }
                var muCols = schema["match_user"];

                string muUserCol  = ResolveName(muCols, "user_id",  "userId");
                string muMatchCol = ResolveName(muCols, "match_id", "matchId");
                if (muUserCol == null || muMatchCol == null)
                {
                    Debug.LogError("[SQL] GetRecentMatches: could not resolve user_id / match_id in match_user.");
                    return results;
                }

                // Columna de resultados opcional en match_user (algunos esquemas la almacenan allí).
                string muResultCol = ResolveName(muCols, "result", "match_result", "outcome");

                // ── matches table ───────────────────────────────────────────────────
                if (!schema.ContainsKey("matches"))
                {
                    Debug.LogError("[SQL] GetRecentMatches: table 'matches' not found. Run LogMatchesSchema().");
                    return results;
                }
                var mCols = schema["matches"];

                string mMatchCol  = ResolveName(mCols, "match_id", "matchId");
                string mDateCol   = ResolveName(mCols, "match_date", "date", "created_at", "timestamp");
                string mResultCol = ResolveName(mCols, "result", "match_result", "outcome");
                string mModeCol   = ResolveName(mCols, "match_type_id", "match_types_id", "type_id",
                                                       "game_mode_id",  "mode_id",
                                                       "match_type",    "match_types", "game_mode");
                string mModalityCol = ResolveName(mCols, "modality", "match_modality", "mode");

                if (mMatchCol == null || mDateCol == null)
                {
                    Debug.LogError("[SQL] GetRecentMatches: could not resolve match_id / date column in matches. Run LogMatchesSchema().");
                    return results;
                }

                // ── match_types table (optional join) ───────────────────────────────
                bool hasMatchTypes = schema.ContainsKey("match_types") && mModeCol != null;
                string mtIdCol   = null;
                string mtNameCol = null;

                // Decide si mModeCol es una clave foránea (entero → unión) o una cadena simple (no se necesita unión).
                bool modeIsFk = false;
                if (hasMatchTypes)
                {
                    var mtCols = schema["match_types"];
                    mtIdCol   = ResolveName(mtCols, "match_type_id", "type_id", "id");
                    mtNameCol = ResolveName(mtCols, "name", "type_name", "match_type", "match_types");
                    // Considérelo una clave foránea solo cuando ambas columnas de la tabla de búsqueda se hayan resuelto correctamente.
                    modeIsFk = (mtIdCol != null && mtNameCol != null);
                }

                // Determinar dónde reside el resultado: preferir match_user, recurrir a matches.
                string resultExpression;
                if (muResultCol != null)
                    resultExpression = $"mu.{muResultCol}";
                else if (mResultCol != null)
                    resultExpression = $"m.{mResultCol}";
                else
                {
                    Debug.LogError("[SQL] GetRecentMatches: could not find a result column in match_user or matches.");
                    return results;
                }

                // Determina la expresión del modo de juego.
                string modeExpression;
                if (mModeCol == null)
                    modeExpression = "NULL";
                else if (modeIsFk)
                    modeExpression = $"mt.{mtNameCol}";
                else
                    modeExpression = $"m.{mModeCol}";

                // Expresión de modalidad (anulable — NULL cuando la columna está ausente).
                string modalityExpression = mModalityCol != null ? $"m.{mModalityCol}" : "NULL";

                // Construye la cláusula JOIN.
                string joinMatchTypes = modeIsFk
                    ? $"LEFT JOIN match_types mt ON mt.{mtIdCol} = m.{mModeCol} "
                    : string.Empty;

                string query =
                    $"SELECT m.{mMatchCol} AS match_id, " +
                    $"       m.{mDateCol}  AS match_date, " +
                    $"       {resultExpression} AS result, " +
                    $"       {modeExpression}   AS game_mode, " +
                    $"       {modalityExpression} AS modality " +
                    $"FROM match_user mu " +
                    $"INNER JOIN matches m ON m.{mMatchCol} = mu.{muMatchCol} " +
                    joinMatchTypes +
                    $"WHERE mu.{muUserCol} = @userId " +
                    $"ORDER BY m.{mDateCol} DESC, m.{mMatchCol} DESC " +
                    "LIMIT 20";

                Debug.Log($"[SQL] GetRecentMatches query: {query}");

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@userId", userId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new MatchEntry
                            {
                                MatchId   = reader.GetInt32("match_id"),
                                Date      = reader.GetDateTime("match_date"),
                                Result    = reader.IsDBNull(reader.GetOrdinal("result"))
                                                ? string.Empty
                                                : reader.GetString("result"),
                                MatchType = reader.IsDBNull(reader.GetOrdinal("game_mode"))
                                                ? string.Empty
                                                : reader.GetString("game_mode"),
                                Modality  = reader.IsDBNull(reader.GetOrdinal("modality"))
                                                ? string.Empty
                                                : reader.GetString("modality"),
                            });
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SQL] GetRecentMatches exception: {ex.Message}\n{ex.StackTrace}");
        }

        return results;
    }

    // Devuelve el nombre de la primera columna candidata que existe en la tabla de coincidencias, o null.
    private string ResolveMatchesColumn(MySqlConnection connection, params string[] candidates)
    {
        var existing = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        using (var cmd = new MySqlCommand("DESCRIBE matches", connection))
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                existing.Add(reader.GetString("Field"));

        foreach (string candidate in candidates)
            if (existing.Contains(candidate))
                return candidate;

        return null;
    }

    /// <summary>
    /// Devuelve todos los participantes en la partida dada.
    /// Combina match_user → users → user_brawlers mediante la detección de esquema en tiempo de ejecución,
    /// exactamente igual que GetRecentMatches, por lo que las variaciones en los nombres de las columnas se gestionan automáticamente.
    /// Cada entrada incluye user_id, nickname, brawler_id, trofeos, nivel y resultado.
    /// </summary>
    public List<MatchParticipantEntry> GetMatchParticipants(int matchId)
    {
        var results = new List<MatchParticipantEntry>();

        try
        {
            using (var connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();

                var schema = BuildSchema(connection);

                // ── match_user ────────────────────────────────────────────────────
                if (!schema.ContainsKey("match_user"))
                {
                    Debug.LogError("[SQL] GetMatchParticipants: table 'match_user' not found.");
                    return results;
                }
                var muCols = schema["match_user"];

                string muMatchCol  = ResolveName(muCols, "match_id",  "matchId");
                string muUserCol   = ResolveName(muCols, "user_id",   "userId");
                string muBrawlCol  = ResolveName(muCols, "brawler_id","brawlerId");
                string muResultCol = ResolveName(muCols, "result", "match_result", "outcome");

                if (muMatchCol == null || muUserCol == null)
                {
                    Debug.LogError("[SQL] GetMatchParticipants: could not resolve match_id / user_id in match_user.");
                    return results;
                }

                // ── users ─────────────────────────────────────────────────────────
                if (!schema.ContainsKey("users"))
                {
                    Debug.LogError("[SQL] GetMatchParticipants: table 'users' not found.");
                    return results;
                }
                var uCols = schema["users"];
                string uUserCol     = ResolveName(uCols, "user_id",  "userId");
                string uNicknameCol = ResolveName(uCols, "nickname", "name", "username");

                if (uUserCol == null || uNicknameCol == null)
                {
                    Debug.LogError("[SQL] GetMatchParticipants: could not resolve user_id / nickname in users.");
                    return results;
                }

                // ── user_brawlers (optional) ──────────────────────────────────────
                bool hasUserBrawlers = schema.ContainsKey("user_brawlers");
                string ubUserCol   = null;
                string ubBrawlCol  = null;
                string ubTrophCol  = null;
                string ubLevelCol  = null;

                if (hasUserBrawlers)
                {
                    var ubCols = schema["user_brawlers"];
                    ubUserCol  = ResolveName(ubCols, "user_id",   "userId");
                    ubBrawlCol = ResolveName(ubCols, "brawler_id","brawlerId");
                    ubTrophCol = ResolveName(ubCols, "trophies",  "trophy_count");
                    ubLevelCol = ResolveName(ubCols, "level",     "brawler_level");
                    hasUserBrawlers = (ubUserCol != null && ubBrawlCol != null);
                }

                // Resolver la fuente brawler_id: preferir match_user, recurrir a user_brawlers.
                string brawlerIdExpression = muBrawlCol != null
                    ? $"mu.{muBrawlCol}"
                    : (hasUserBrawlers ? $"ub.{ubBrawlCol}" : "0");

                // Resolver origen del resultado: preferir la columna match_user.
                string resultExpression = muResultCol != null
                    ? $"mu.{muResultCol}"
                    : "NULL";

                string trophiesExpression = (hasUserBrawlers && ubTrophCol != null)
                    ? $"ub.{ubTrophCol}"
                    : "0";
                string levelExpression = (hasUserBrawlers && ubLevelCol != null)
                    ? $"ub.{ubLevelCol}"
                    : "1";

                string joinUserBrawlers = hasUserBrawlers && muBrawlCol != null
                    ? $"LEFT JOIN user_brawlers ub ON ub.{ubUserCol} = mu.{muUserCol} AND ub.{ubBrawlCol} = mu.{muBrawlCol} "
                    : string.Empty;

                string query =
                    $"SELECT mu.{muUserCol} AS user_id, " +
                    $"       u.{uNicknameCol} AS nickname, " +
                    $"       {brawlerIdExpression} AS brawler_id, " +
                    $"       {trophiesExpression}  AS trophies, " +
                    $"       {levelExpression}     AS level, " +
                    $"       {resultExpression}    AS result " +
                    $"FROM match_user mu " +
                    $"INNER JOIN users u ON u.{uUserCol} = mu.{muUserCol} " +
                    joinUserBrawlers +
                    $"WHERE mu.{muMatchCol} = @matchId";

                Debug.Log($"[SQL] GetMatchParticipants query: {query}");

                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@matchId", matchId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new MatchParticipantEntry
                            {
                                UserId    = reader.GetString("user_id"),
                                Nickname  = reader.GetString("nickname"),
                                BrawlerId = reader.IsDBNull(reader.GetOrdinal("brawler_id")) ? 0 : reader.GetInt32("brawler_id"),
                                Trophies  = reader.IsDBNull(reader.GetOrdinal("trophies"))   ? 0 : reader.GetInt32("trophies"),
                                Level     = reader.IsDBNull(reader.GetOrdinal("level"))       ? 1 : reader.GetInt32("level"),
                                Result    = reader.IsDBNull(reader.GetOrdinal("result"))
                                                ? string.Empty
                                                : reader.GetString("result"),
                            });
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SQL] GetMatchParticipants exception: {ex.Message}\n{ex.StackTrace}");
        }

        return results;
    }

    /// <summary>
    /// Devuelve todos los brawlers desbloqueados por el usuario especificado, junto con la tabla de brawlers.
    /// para obtener el nombre del brawler, sus trofeos y su nivel.
    /// </summary>
    public List<BrawlerEntry> GetUnlockedBrawlers(string userId)
    {
        var results = new List<BrawlerEntry>();

        using (var connection = new MySqlConnection(ConnectionString))
        {
            connection.Open();

            const string query =
                "SELECT b.brawler_id, b.name, ub.trophies, ub.level " +
                "FROM user_brawlers ub " +
                "INNER JOIN brawlers b ON b.brawler_id = ub.brawler_id " +
                "WHERE ub.user_id = @userId " +
                "ORDER BY ub.trophies DESC";

            using (var command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new BrawlerEntry
                        {
                            BrawlerId = reader.GetInt32("brawler_id"),
                            Name      = reader.GetString("name"),
                            Trophies  = reader.GetInt32("trophies"),
                            Level     = reader.GetInt32("level"),
                        });
                    }
                }
            }
        }

        return results;
    }
}
