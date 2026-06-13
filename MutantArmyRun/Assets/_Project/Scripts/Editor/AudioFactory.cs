using System.IO;
using MutantArmy.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MutantArmy.Editor
{
    /// <summary>
    /// MAR Tools/Build Audio — dono do pipeline de ÁUDIO por código (separado do Build Juice,
    /// que cobre VFX/partículas e ainda escreve o subconjunto antigo do catálogo de forma
    /// compatível). Idempotente: re-rodar atualiza no lugar, nunca duplica.
    ///
    /// 1. Importa os .ogg CC0 do staging (PLANO-DE-USO §1.7) para Assets/_Project/Audio com
    ///    nomes canônicos por evento — inclui os novos footstep/explosão e as faixas de mundo.
    ///    Fonte ausente vira aviso (nunca erro): o campo correspondente fica null (no-op).
    /// 2. Preenche o AudioCatalog.asset COMPLETO (SFX por evento + musicByWorld[]), incluindo os
    ///    slots da missão Nota 10 (weaknessHit/resistedHit/specialWarning/bossDeath/comboSting/
    ///    coinBurst/enemyPop/goodChoice/badChoice/riskWin/riskLose). Cria o asset se não existir.
    ///    Clip ausente = null = evento mudo (contrato do AudioManager).
    /// 3. Música de fundo (Lacuna L7 — TETO HONESTO: o staging CC0 só tem JINGLES curtos, todos
    ///    &lt;2 s; NÃO há loops/trilhas dedicadas). Estratégia: aponta os jingles mais MELÓDICOS e
    ///    LONGOS (famílias Steel/Sax/8-Bit/Pizzicato — NÃO os stingers "Hit", que têm transiente
    ///    seco e emenda horrível em loop) para tocar EM LOOP como ambiente leve; o AudioManager
    ///    faz crossfade na troca de faixa para suavizar a emenda. menuMusic para as telas de menu,
    ///    musicByWorld[worldIndex] por mundo (1-based: W01=1, W02=2, W03=3 — slot 0 vago).
    ///    Documentado abaixo em <see cref="MusicByWorldMap"/>. PENDÊNCIA: música real por mundo
    ///    precisa de trilhas dedicadas — trocar é só reapontar estas fontes.
    /// 4. Costura a cena Boot: catálogo no AudioManager, 2 AudioSources (música+SFX) e o
    ///    AudioListener garantidos (o ProjectSetup já põe; reforçamos por robustez).
    /// </summary>
    public static class AudioFactory
    {
        private const string AudioFolder = "Assets/_Project/Audio";
        private const string CatalogFolder = "Assets/_Project/ScriptableObjects/Audio";
        private const string CatalogPath = CatalogFolder + "/AudioCatalog.asset";
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        // staging fica AO LADO da pasta do projeto: <raiz>\_assets-staging (igual ao JuiceFactory)
        private static string StagingRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_assets-staging"));

        // destino canônico (por evento) → origem no staging (PLANO-DE-USO §1.7, tudo CC0 1.0).
        // Os 12 primeiros casam com o que o Build Juice já importa (idempotente, não re-copia);
        // os de baixo (footstep/explosão/músicas de mundo) são os que este factory acrescenta.
        private static readonly (string dest, string source)[] AudioMap =
        {
            // --- SFX por evento (compatível com o Build Juice) ---
            ("sfx_gate_positive.ogg",  @"audio\kenney_digital-audio\Audio\phaserUp4.ogg"),
            ("sfx_gate_negative.ogg",  @"audio\kenney_digital-audio\Audio\phaserDown1.ogg"),
            ("sfx_coin.ogg",           @"audio\kenney_rpg-audio\Audio\handleCoins.ogg"),
            ("sfx_pop.ogg",            @"audio\kenney_digital-audio\Audio\pepSound3.ogg"),
            ("sfx_boss_hit.ogg",       @"audio\kenney_impact-sounds\Audio\impactPunch_heavy_001.ogg"),
            ("sfx_boss_roar.ogg",      @"audio\kenney_sci-fi-sounds\Audio\lowFrequency_explosion_000.ogg"), // Lacuna L6
            ("sfx_ui_click.ogg",       @"audio\kenney_interface-sounds\Audio\click_002.ogg"),
            ("sfx_ui_confirm.ogg",     @"audio\kenney_interface-sounds\Audio\confirmation_002.ogg"),
            ("sfx_supply_fanfare.ogg", @"audio\kenney_digital-audio\Audio\powerUp1.ogg"),
            ("sfx_mutation.ogg",       @"audio\kenney_digital-audio\Audio\powerUp6.ogg"),
            ("jingle_victory.ogg",     @"audio\kenney_music-jingles\Audio\Hit jingles\jingles_HIT16.ogg"),
            ("jingle_defeat.ogg",      @"audio\kenney_music-jingles\Audio\Hit jingles\jingles_HIT04.ogg"),

            // --- Novos: passos da multidão e explosão (PLANO §1.7) ---
            ("sfx_footstep.ogg",       @"audio\kenney_impact-sounds\Audio\footstep_grass_000.ogg"),
            ("sfx_explosion.ogg",      @"audio\kenney_sci-fi-sounds\Audio\explosionCrunch_001.ogg"),

            // --- Missão Nota 10: boss elemental / combos / inimigos / veredito de escolha ---
            // (tudo CC0 do staging; reuso de famílias já aprovadas, variações ainda não usadas)
            ("sfx_weakness_hit.ogg",    @"audio\kenney_impact-sounds\Audio\impactBell_heavy_002.ogg"),   // sino brilhante: "acertou a fraqueza"
            ("sfx_resisted_hit.ogg",    @"audio\kenney_impact-sounds\Audio\impactMetal_heavy_000.ogg"),  // "bateu em parede" seco
            ("sfx_special_warning.ogg", @"audio\kenney_digital-audio\Audio\threeTone2.ogg"),             // alarme curto do telegraph
            ("sfx_boss_death.ogg",      @"audio\kenney_sci-fi-sounds\Audio\explosionCrunch_004.ogg"),    // crunch MAIS longo/pesado (~2s) — morte cinematográfica
            ("sfx_combo_sting.ogg",     @"audio\kenney_digital-audio\Audio\powerUp3.ogg"),               // sting de combo conquistado
            ("sfx_coin_burst.ogg",      @"audio\kenney_rpg-audio\Audio\handleCoins2.ogg"),               // chuva de moedas (wave limpa)
            ("sfx_enemy_pop.ogg",       @"audio\kenney_digital-audio\Audio\pepSound2.ogg"),              // pop de inimigo de pista
            ("sfx_good_choice.ogg",     @"audio\kenney_interface-sounds\Audio\confirmation_001.ogg"),    // BOA ESCOLHA!
            ("sfx_bad_choice.ogg",      @"audio\kenney_interface-sounds\Audio\error_004.ogg"),           // portal armadilha
            ("sfx_risk_win.ogg",        @"audio\kenney_digital-audio\Audio\powerUp8.ogg"),               // risco vencido ("x10!")
            ("sfx_risk_lose.ogg",       @"audio\kenney_digital-audio\Audio\lowDown.ogg"),                // risco perdido (descida seca)

            // --- Música de fundo (Lacuna L7 — TETO HONESTO: o staging CC0 só tem JINGLES <2s,
            //     não há loops dedicados). Trocados dos stingers "Hit" (transientes secos, loop
            //     horrível) para as famílias MELÓDICAS e mais LONGAS (Steel/Sax/8-Bit/Pizzicato)
            //     — corpo sustentado emenda menos mal em loop. O AudioManager faz crossfade na
            //     troca de faixa para suavizar ainda mais. Ver pendência: música real precisa de
            //     trilhas dedicadas (CC0 só tem jingles).
            ("music_menu.ogg",         @"audio\kenney_music-jingles\Audio\Pizzicato jingles\jingles_PIZZI07.ogg"), // menu: pizzicato suave/acolhedor (~1.3s, o mais calmo)
            ("music_world_01.ogg",     @"audio\kenney_music-jingles\Audio\Steel jingles\jingles_STEEL07.ogg"),     // W01 Campo: steel brilhante/heroico (~1.55s, o mais cheio)
            ("music_world_02.ogg",     @"audio\kenney_music-jingles\Audio\Sax jingles\jingles_SAX07.ogg"),         // W02 Zumbi: sax mais sombrio/grave (~1.74s)
            ("music_world_03.ogg",     @"audio\kenney_music-jingles\Audio\8-Bit jingles\jingles_NES00.ogg"),       // W03 Robótico: chiptune NES = "tech" (~1.76s)
        };

        // worldIndex (1-based nos assets do MVP) → nome canônico da faixa importada.
        // O array musicByWorld é dimensionado para indexar DIRETO por worldIndex (slot 0 vago).
        private static readonly (int worldIndex, string clipName)[] MusicByWorldMap =
        {
            (1, "music_world_01"),   // W01 Campo Inicial    — Steel: brilhante/heroico
            (2, "music_world_02"),   // W02 Cidade Zumbi     — Sax: mais sombrio/grave
            (3, "music_world_03"),   // W03 Deserto Robótico — 8-Bit/NES: chiptune "tech"
        };

        [MenuItem("MAR Tools/Build Audio")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("MAR Tools: Build Audio não roda em play mode.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(AudioFolder);
            EnsureFolder(CatalogFolder);

            ImportAudioFromStaging();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AudioCatalogSO catalog = BuildCatalog();
            WireBootScene(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MAR Tools: áudio pronto — clips importados + catálogo completo " +
                      "(SFX por evento + " + MusicByWorldMap.Length + " faixas de mundo em loop, " +
                      "Lacuna L7) e cena Boot costurada.");
        }

        // ------------------------------------------------------------------ 1. import do staging

        private static void ImportAudioFromStaging()
        {
            string staging = StagingRoot;
            if (!Directory.Exists(staging))
            {
                Debug.LogWarning("MAR Tools: staging não encontrado em " + staging +
                                 " — clips ficarão nulos (fallback silencioso).");
                return;
            }

            foreach ((string dest, string source) in AudioMap)
            {
                string sourcePath = Path.Combine(staging, source);
                string destPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AudioFolder, dest));
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning("MAR Tools: fonte ausente no staging: " + source + " — '" + dest + "' pulado.");
                    continue;
                }
                if (File.Exists(destPath)) continue;   // idempotente: nunca re-copia (preserva tweaks)
                File.Copy(sourcePath, destPath);
            }
        }

        // ------------------------------------------------------------------ 2/3. catálogo completo

        private static AudioCatalogSO BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            // SFX por evento (todos opcionais — ausência vira null = no-op no AudioManager).
            catalog.gatePositive = LoadClip("sfx_gate_positive");
            catalog.gateNegative = LoadClip("sfx_gate_negative");
            catalog.coin = LoadClip("sfx_coin");
            catalog.pop = LoadClip("sfx_pop");
            catalog.supplyFanfare = LoadClip("sfx_supply_fanfare");
            catalog.footstep = LoadClip("sfx_footstep");
            catalog.bossHit = LoadClip("sfx_boss_hit");
            catalog.bossRoar = LoadClip("sfx_boss_roar");
            catalog.explosion = LoadClip("sfx_explosion");
            catalog.mutation = LoadClip("sfx_mutation");
            catalog.uiClick = LoadClip("sfx_ui_click");
            catalog.uiConfirm = LoadClip("sfx_ui_confirm");
            catalog.victoryJingle = LoadClip("jingle_victory");
            catalog.defeatJingle = LoadClip("jingle_defeat");

            // Slots da missão Nota 10 (boss elemental, combos, inimigos de pista, veredito de
            // escolha) — mesma regra de contrato: clip ausente = null = evento mudo.
            catalog.weaknessHit = LoadClip("sfx_weakness_hit");
            catalog.resistedHit = LoadClip("sfx_resisted_hit");
            catalog.specialWarning = LoadClip("sfx_special_warning");
            catalog.bossDeath = LoadClip("sfx_boss_death");
            catalog.comboSting = LoadClip("sfx_combo_sting");
            catalog.coinBurst = LoadClip("sfx_coin_burst");
            catalog.enemyPop = LoadClip("sfx_enemy_pop");
            catalog.goodChoice = LoadClip("sfx_good_choice");
            catalog.badChoice = LoadClip("sfx_bad_choice");
            catalog.riskWin = LoadClip("sfx_risk_win");
            catalog.riskLose = LoadClip("sfx_risk_lose");

            catalog.menuMusic = LoadClip("music_menu");   // música de fundo do menu/meta (loop)
            catalog.musicByWorld = BuildMusicByWorld();

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // Array indexado DIRETO por worldIndex: tamanho = maior worldIndex + 1 (slot 0 vago).
        private static AudioClip[] BuildMusicByWorld()
        {
            int maxIndex = 0;
            foreach ((int worldIndex, string _) in MusicByWorldMap)
                if (worldIndex > maxIndex) maxIndex = worldIndex;

            var music = new AudioClip[maxIndex + 1];
            foreach ((int worldIndex, string clipName) in MusicByWorldMap)
                music[worldIndex] = LoadClip(clipName);
            return music;
        }

        // clip ausente fica NULL — o AudioManager trata como no-op silencioso (contrato)
        private static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".ogg");
            if (clip == null)
                Debug.LogWarning("MAR Tools: clip '" + name + "' não importado — evento ficará mudo (fallback nulo).");
            return clip;
        }

        // ------------------------------------------------------------------ 4. cena Boot

        private static void WireBootScene(AudioCatalogSO catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) == null)
            {
                Debug.LogWarning("MAR Tools: cena Boot ausente em " + BootScenePath +
                                 " — rode MAR Tools/Setup Project antes do Build Audio.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            var audio = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (audio == null)
            {
                Debug.LogError("MAR Tools: AudioManager não existe na cena Boot — rode MAR Tools/Setup Project.");
            }
            else
            {
                WireField(audio, "_catalog", catalog);
                // Legado (fallback): mantém os campos antigos preenchidos para compatibilidade
                // com cenas montadas antes do catálogo ganhar os novos campos.
                WireField(audio, "_supplyFanfareClip", LoadClip("sfx_supply_fanfare"));
                WireField(audio, "_mutationClip", LoadClip("sfx_mutation"));
                EnsureAudioSources(audio);
            }

            EnsureAudioListener();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // garante as 2 fontes do prefab [Services] (música + SFX) ligadas nos campos reais
        private static void EnsureAudioSources(AudioManager audio)
        {
            var serialized = new SerializedObject(audio);
            SerializedProperty music = serialized.FindProperty("_musicSource");
            SerializedProperty sfx = serialized.FindProperty("_sfxSource");
            if (music == null || sfx == null)
            {
                Debug.LogError("MAR Tools: campos _musicSource/_sfxSource não existem no AudioManager.");
                return;
            }

            if (music.objectReferenceValue == null)
            {
                AudioSource source = audio.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                music.objectReferenceValue = source;
            }
            if (sfx.objectReferenceValue == null)
            {
                AudioSource source = audio.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfx.objectReferenceValue = source;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) != null) return;
            Camera cam = Camera.main;
            if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            else Debug.LogWarning("MAR Tools: cena sem Camera.main — AudioListener não garantido.");
        }

        // ------------------------------------------------------------------ infra

        /// <summary>Liga campo [SerializeField] por nome — campo inexistente é ERRO explícito.</summary>
        private static void WireField(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    $"MAR Tools: campo serializado '{fieldName}' não existe em {target.GetType().Name} — wiring ignorado.",
                    target);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
