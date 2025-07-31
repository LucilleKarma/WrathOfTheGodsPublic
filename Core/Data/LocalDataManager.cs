using System.Collections;
using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace NoxusBoss.Core.Data;

public sealed class LocalDataManager : ModSystem
{
    private static bool writingFile;

    /// <summary>
    /// The set of all data references to observe.
    /// </summary>
    internal static readonly Dictionary<string, WatchedData> DataReferences = [];

    /// <summary>
    /// The path in which all watched data should be kept in.
    /// </summary>
    public static string DataPath
    {
        get
        {
            string? dataPath = Environment.GetEnvironmentVariable(DataPathEnvironmentVariableName, EnvironmentVariableTarget.User);
            if (dataPath is null)
            {
                Environment.SetEnvironmentVariable(DataPathEnvironmentVariableName, DefaultDataPath, EnvironmentVariableTarget.User);
                return DefaultDataPath;
            }

            return dataPath;
        }
    }

    /// <summary>
    /// The file watcher responsible for keeping data up to date as it's updated while the game is running.
    /// </summary>
    public static FileSystemWatcher? DataWatcher
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether file watching is enabled or not.
    /// </summary>
    public static bool WatchingIsEnabled => true;

    /// <summary>
    /// The default data path to use if the environment variable is uninitialized.
    /// </summary>
    public static string DefaultDataPath => Path.Combine(Main.SavePath, "Dev");

    /// <summary>
    /// The environment variable name for the data path.
    /// </summary>
    public const string DataPathEnvironmentVariableName = "WoTGDataPath";

    /// <summary>
    /// The color of text displayed in the game chat indicating successful updating of a JSON file.
    /// </summary>
    public static readonly Color SuccessColor = new Color(54, 187, 107);

    /// <summary>
    /// Determines whether a given data/path set should generate a new local file for the user, in accordance with the <see cref=""/>.
    /// </summary>
    /// <param name="path">The data's relative path.</param>
    /// <param name="data">The data to reference.</param>
    private static bool ShouldGenerateNewLocalFile(string path, Dictionary<string, object> data)
    {
#if !DEBUG
        return false;
#endif

        if (!WatchingIsEnabled)
            return false;

        // Check if the data already has a copy in the data path. If it does not, generate it.
        // Also, if it does but is old (aka doesn't have all the required keys) or malformed, regenerate it.
        bool generateDataCopy = !File.Exists(path);
        if (!generateDataCopy)
        {
            Dictionary<string, object>? existingData = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
            if (existingData is null)
                generateDataCopy = true;
            else if (!existingData.Keys.SequenceEqual(data.Keys))
                generateDataCopy = true;
        }

        return generateDataCopy;
    }

    /// <summary>
    /// Registers a watched data reference.
    /// </summary>
    /// <typeparam name="TJsonData">The type of data to create a reference for.</typeparam>
    /// <param name="path">The data's relative path.</param>
    /// <param name="data">The data to reference.</param>
    private static void RegisterReference<TJsonData>(string path, Dictionary<string, object> data)
    {
        WatchedData reference = new WatchedData(data, typeof(Dictionary<string, TJsonData>));

        if (!Directory.Exists(DataPath))
            Directory.CreateDirectory(DataPath);

        string fileName = Path.GetFileName(path);
        string locationInDataPath = Path.Combine(DataPath, fileName);

        writingFile = true;

        bool generateDataCopy = ShouldGenerateNewLocalFile(locationInDataPath, data);
        if (generateDataCopy)
        {
            if (File.Exists(locationInDataPath))
                File.Delete(locationInDataPath);

            string dataJson = JsonConvert.SerializeObject(data, Formatting.Indented);

            using FileStream fileStream = File.OpenWrite(locationInDataPath);
            fileStream.Write(Encoding.Default.GetBytes(dataJson));
            fileStream.Close();

            // Wait a short moment so that the file handles can be released, to ensure that the LogFileChanges event is not mistakenly fired.
            Thread.Sleep(350);
        }

        writingFile = false;

        DataReferences[fileName] = reference;
    }

    private static void LogFileChanges(object sender, FileSystemEventArgs e)
    {
        if (Main.netMode != NetmodeID.SinglePlayer)
            return;

        string fileName = Path.GetFileName(e.Name ?? string.Empty);
        if (!DataReferences.TryGetValue(fileName, out WatchedData? oldData))
            return;

        // Temporarily turn off recording if the file access was actually due to a reference being registered and not due to a change by the user.
        // Not doing this causes the game to immediately crash.
        if (writingFile || Main.gameMenu)
            return;

        try
        {
            string text = File.ReadAllText(e.FullPath);
            object? jsonObject = JsonConvert.DeserializeObject(text, oldData.IntendedType);
            IDictionary? manualCast = jsonObject as IDictionary;
            Dictionary<string, object> data = [];

            if (manualCast is not null)
            {
                foreach (DictionaryEntry entry in manualCast)
                {
                    if (entry.Key is string key && entry.Value is not null)
                        data[key] = entry.Value;
                }
            }

            if (data.Count >= 1)
            {
                DataReferences[fileName] = new(data, oldData.IntendedType);
            }
        }

        // Ignore exceptions pertaining to file handles.
        catch (IOException)
        {

        }
    }

    /// <summary>
    /// Acquires JSON data from a given path in the mod, registering it for use with the <see cref="DataWatcher"/> if necessary.
    /// </summary>
    /// <typeparam name="TJsonData">The type of data to search for.</typeparam>
    /// <param name="path">The relative path of the data in the mod's source.</param>
    /// <exception cref="FileNotFoundException"></exception>
    public static Dictionary<string, TJsonData> Read<TJsonData>(string path)
    {
        // Check if a reference was already stored. If it was, use it instead for efficiency.
        string fileName = Path.GetFileName(path);
        if (DataReferences.TryGetValue(fileName, out WatchedData? storedData))
        {
            Dictionary<string, TJsonData>? manualCast = storedData.Data as Dictionary<string, TJsonData>;
            if (manualCast is not null)
                return manualCast;

            Dictionary<string, TJsonData> convertedDictionary = [];
            foreach (KeyValuePair<string, object> kv in storedData.Data)
                convertedDictionary[kv.Key] = (TJsonData)kv.Value;

            return convertedDictionary;
        }

        string fileText = Encoding.Default.GetString(ModContent.GetInstance<NoxusBoss>().GetFileBytes(path));
        Dictionary<string, TJsonData>? data = JsonConvert.DeserializeObject<Dictionary<string, TJsonData>>(fileText);
        if (data is null)
            throw new FileNotFoundException($"Could not locate the file at {path}.");
        else
            RegisterReference<TJsonData>(path, data.Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value!)).ToDictionary());

        return data;
    }

    public override void OnModLoad()
    {
        if (Main.netMode != NetmodeID.SinglePlayer)
            return;
#if !DEBUG
        return;
#endif

        if (!Directory.Exists(DataPath))
            Directory.CreateDirectory(DataPath);

        DataWatcher = new(DataPath, "*.json")
        {
            NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Security,
            EnableRaisingEvents = true
        };
        DataWatcher.Changed += LogFileChanges;
    }

    public override void OnModUnload() => DataWatcher?.Dispose();
}
