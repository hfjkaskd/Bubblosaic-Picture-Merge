using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Immutable data mirror of the ServerAB document bundled with 1.0.9.
    /// This type deliberately contains no bucket-selection or condition-
    /// evaluation behavior: it preserves the server contract for consumers
    /// that already have an authoritative experiment assignment.
    /// </summary>
    public sealed class AbTestConfig
    {
        internal AbTestConfig(
            int versionCode,
            IReadOnlyList<AbTestFlowDomain> flowDomains,
            IReadOnlyList<AbTestLayer> layers,
            AbTestV5Config v5Config,
            IReadOnlyDictionary<string, AbTestJsonValue>
                experimentDependencyMap,
            IReadOnlyList<AbTestExperiment> experiments,
            IReadOnlyList<AbTestStrategy> strategies,
            IReadOnlyList<AbTestJsonValue> combinedLayers,
            IReadOnlyList<AbTestJsonValue> parameterExcludeRules)
        {
            VersionCode = versionCode;
            FlowDomains = flowDomains;
            Layers = layers;
            V5Config = v5Config;
            ExperimentDependencyMap = experimentDependencyMap;
            Experiments = experiments;
            Strategies = strategies;
            CombinedLayers = combinedLayers;
            ParameterExcludeRules = parameterExcludeRules;
            UniqueParameterKeys = BuildUniqueParameterKeys(
                experiments,
                strategies);
        }

        public int VersionCode { get; }
        public IReadOnlyList<AbTestFlowDomain> FlowDomains { get; }
        public IReadOnlyList<AbTestLayer> Layers { get; }
        public AbTestV5Config V5Config { get; }

        /// <summary>
        /// Dependency entries keyed exactly as delivered by ServerAB. Values
        /// use a discriminated JSON representation because the bundled 1.0.9
        /// map is empty and therefore does not reveal a narrower value schema.
        /// </summary>
        public IReadOnlyDictionary<string, AbTestJsonValue>
            ExperimentDependencyMap { get; }

        public IReadOnlyList<AbTestExperiment> Experiments { get; }
        public IReadOnlyList<AbTestStrategy> Strategies { get; }

        /// <summary>
        /// Forward-compatible mirrors of the currently empty ServerAB arrays.
        /// </summary>
        public IReadOnlyList<AbTestJsonValue> CombinedLayers { get; }
        public IReadOnlyList<AbTestJsonValue> ParameterExcludeRules { get; }

        /// <summary>
        /// Ordinally sorted union of experiment group-data keys and strategy
        /// parameter keys. This is descriptive metadata, not a resolved AB
        /// assignment.
        /// </summary>
        public IReadOnlyList<string> UniqueParameterKeys { get; }

        static IReadOnlyList<string> BuildUniqueParameterKeys(
            IReadOnlyList<AbTestExperiment> experiments,
            IReadOnlyList<AbTestStrategy> strategies)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < experiments.Count; i++)
            {
                IReadOnlyList<AbTestExperimentGroup> groups =
                    experiments[i].Groups;
                for (int g = 0; g < groups.Count; g++)
                {
                    foreach (string key in groups[g].GroupData.Keys)
                        keys.Add(key);
                }
            }

            for (int i = 0; i < strategies.Count; i++)
                keys.Add(strategies[i].ParameterKey);

            string[] result = keys.ToArray();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    public sealed class AbTestFlowDomain
    {
        internal AbTestFlowDomain(
            string id,
            string diversionId,
            string domainTag,
            bool keepHitting,
            IReadOnlyList<string> buckets,
            IReadOnlyList<AbTestFlowDomain> children)
        {
            Id = id;
            DiversionId = diversionId;
            DomainTag = domainTag;
            KeepHitting = keepHitting;
            Buckets = buckets;
            Children = children;
        }

        public string Id { get; }
        public string DiversionId { get; }
        public string DomainTag { get; }
        public bool KeepHitting { get; }
        public IReadOnlyList<string> Buckets { get; }
        public IReadOnlyList<AbTestFlowDomain> Children { get; }
    }

    public sealed class AbTestLayer
    {
        internal AbTestLayer(
            string id,
            string flowDomainId,
            IReadOnlyList<string> buckets)
        {
            Id = id;
            FlowDomainId = flowDomainId;
            Buckets = buckets;
        }

        public string Id { get; }
        public string FlowDomainId { get; }
        public IReadOnlyList<string> Buckets { get; }
    }

    public sealed class AbTestExperiment
    {
        internal AbTestExperiment(
            string id,
            string layerId,
            string tag,
            string tagTime,
            bool isEmptyRunningExperiment,
            bool retainExperimentHit,
            IReadOnlyList<string> buckets,
            AbTestCondition condition,
            IReadOnlyList<AbTestExperimentGroup> groups,
            IReadOnlyDictionary<string, AbTestJsonValue> extraInfo)
        {
            Id = id;
            LayerId = layerId;
            Tag = tag;
            TagTime = tagTime;
            IsEmptyRunningExperiment = isEmptyRunningExperiment;
            RetainExperimentHit = retainExperimentHit;
            Buckets = buckets;
            Condition = condition;
            Groups = groups;
            ExtraInfo = extraInfo;
        }

        public string Id { get; }
        public string LayerId { get; }
        public string Tag { get; }
        public string TagTime { get; }
        public bool IsEmptyRunningExperiment { get; }
        public bool RetainExperimentHit { get; }
        public IReadOnlyList<string> Buckets { get; }
        public AbTestCondition Condition { get; }
        public IReadOnlyList<AbTestExperimentGroup> Groups { get; }
        public IReadOnlyDictionary<string, AbTestJsonValue> ExtraInfo { get; }
    }

    public sealed class AbTestExperimentGroup
    {
        internal AbTestExperimentGroup(
            string tag,
            int index,
            bool isClosed,
            IReadOnlyList<string> historyTags,
            IReadOnlyDictionary<string, string> groupData)
        {
            Tag = tag;
            Index = index;
            IsClosed = isClosed;
            HistoryTags = historyTags;
            GroupData = groupData;
        }

        public string Tag { get; }
        public int Index { get; }
        public bool IsClosed { get; }
        public IReadOnlyList<string> HistoryTags { get; }
        public IReadOnlyDictionary<string, string> GroupData { get; }
    }

    public sealed class AbTestCondition
    {
        internal AbTestCondition(
            string groupType,
            IReadOnlyList<AbTestConditionGroup> groups)
        {
            GroupType = groupType;
            Groups = groups;
        }

        public string GroupType { get; }
        public IReadOnlyList<AbTestConditionGroup> Groups { get; }
    }

    public sealed class AbTestConditionGroup
    {
        internal AbTestConditionGroup(
            string type,
            IReadOnlyList<AbTestConditionRule> rules)
        {
            Type = type;
            Rules = rules;
        }

        public string Type { get; }
        public IReadOnlyList<AbTestConditionRule> Rules { get; }
    }

    public sealed class AbTestConditionRule
    {
        internal AbTestConditionRule(
            string key,
            string valueType,
            string ruleType,
            string operatorType,
            IReadOnlyList<AbTestConditionValue> values)
        {
            Key = key;
            ValueType = valueType;
            RuleType = ruleType;
            OperatorType = operatorType;
            Values = values;
        }

        public string Key { get; }
        public string ValueType { get; }
        public string RuleType { get; }
        public string OperatorType { get; }

        /// <summary>
        /// Preserves heterogeneous JSON arrays such as
        /// [version-string, null] and [{ user-tag rule }].
        /// </summary>
        public IReadOnlyList<AbTestConditionValue> Values { get; }
    }

    public sealed class AbTestConditionValue
    {
        internal AbTestConditionValue(
            AbTestJsonValue jsonValue,
            AbTestUserTagRule userTagRule)
        {
            JsonValue = jsonValue;
            UserTagRule = userTagRule;
        }

        public AbTestJsonValueKind Kind => JsonValue.Kind;
        public AbTestJsonValue JsonValue { get; }

        /// <summary>
        /// Non-null when the object variant has the ServerAB user-tag shape.
        /// The original object remains available through JsonValue.
        /// </summary>
        public AbTestUserTagRule UserTagRule { get; }

        public string StringValue => JsonValue.StringValue;
        public double NumberValue => JsonValue.NumberValue;
        public bool BooleanValue => JsonValue.BooleanValue;
    }

    public sealed class AbTestUserTagRule
    {
        internal AbTestUserTagRule(
            string valueType,
            string ruleType,
            string operatorType,
            string tagId,
            IReadOnlyList<string> tagValues,
            bool isFullMatch,
            bool isList)
        {
            ValueType = valueType;
            RuleType = ruleType;
            OperatorType = operatorType;
            TagId = tagId;
            TagValues = tagValues;
            IsFullMatch = isFullMatch;
            IsList = isList;
        }

        public string ValueType { get; }
        public string RuleType { get; }
        public string OperatorType { get; }
        public string TagId { get; }
        public IReadOnlyList<string> TagValues { get; }
        public bool IsFullMatch { get; }
        public bool IsList { get; }
    }

    public sealed class AbTestStrategy
    {
        internal AbTestStrategy(
            string flowDomainId,
            string parameterKey,
            string parameterValue,
            string tag,
            int priority,
            AbTestCondition condition,
            IReadOnlyDictionary<string, AbTestJsonValue> extraInfo)
        {
            FlowDomainId = flowDomainId;
            ParameterKey = parameterKey;
            ParameterValue = parameterValue;
            Tag = tag;
            Priority = priority;
            Condition = condition;
            ExtraInfo = extraInfo;
        }

        public string FlowDomainId { get; }
        public string ParameterKey { get; }
        public string ParameterValue { get; }
        public string Tag { get; }
        public int Priority { get; }
        public AbTestCondition Condition { get; }
        public IReadOnlyDictionary<string, AbTestJsonValue> ExtraInfo { get; }
    }

    public sealed class AbTestV5Config
    {
        internal AbTestV5Config(
            int versionCode,
            IReadOnlyList<AbTestJsonValue> layers,
            IReadOnlyList<AbTestJsonValue> parameters,
            IReadOnlyList<AbTestJsonValue> flowDomainParameters)
        {
            VersionCode = versionCode;
            Layers = layers;
            Parameters = parameters;
            FlowDomainParameters = flowDomainParameters;
        }

        public int VersionCode { get; }
        public IReadOnlyList<AbTestJsonValue> Layers { get; }
        public IReadOnlyList<AbTestJsonValue> Parameters { get; }
        public IReadOnlyList<AbTestJsonValue> FlowDomainParameters { get; }
    }

    public enum AbTestJsonValueKind
    {
        Null,
        String,
        Number,
        Boolean,
        Object,
        Array,
    }

    /// <summary>
    /// Lossless, strongly discriminated representation for ServerAB fields
    /// whose schema is intentionally extensible (dependency map and metadata).
    /// </summary>
    public sealed class AbTestJsonValue
    {
        static readonly IReadOnlyDictionary<string, AbTestJsonValue>
            EmptyObject = new ReadOnlyDictionary<string, AbTestJsonValue>(
                new Dictionary<string, AbTestJsonValue>(
                    StringComparer.Ordinal));

        internal AbTestJsonValue(
            AbTestJsonValueKind kind,
            string stringValue,
            double numberValue,
            bool booleanValue,
            IReadOnlyDictionary<string, AbTestJsonValue> objectValue,
            IReadOnlyList<AbTestJsonValue> arrayValue)
        {
            Kind = kind;
            StringValue = stringValue;
            NumberValue = numberValue;
            BooleanValue = booleanValue;
            ObjectValue = objectValue ?? EmptyObject;
            ArrayValue = arrayValue ?? Array.Empty<AbTestJsonValue>();
        }

        public AbTestJsonValueKind Kind { get; }
        public string StringValue { get; }
        public double NumberValue { get; }
        public bool BooleanValue { get; }
        public IReadOnlyDictionary<string, AbTestJsonValue> ObjectValue
        {
            get;
        }

        public IReadOnlyList<AbTestJsonValue> ArrayValue { get; }
    }

    /// <summary>
    /// Loads and parses the bundled AB data without evaluating conditions or
    /// selecting buckets. Experiment assignments must come from ServerAB.
    /// </summary>
    public static class AbTestConfigRepository
    {
        public const string BundledResourcePath = "Config/abtest_config";

        public static AbTestConfig LoadBundled()
        {
            TextAsset asset = Resources.Load<TextAsset>(BundledResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Bundled AB config is missing at Resources/" +
                    BundledResourcePath + ".json.");
            }

            if (!TryParse(asset.text, out AbTestConfig config, out string error))
            {
                throw new FormatException(
                    "Bundled AB config could not be parsed: " + error);
            }

            return config;
        }

        public static bool TryParse(
            string json,
            out AbTestConfig config)
        {
            return TryParse(json, out config, out _);
        }

        public static bool TryParse(
            string json,
            out AbTestConfig config,
            out string error)
        {
            config = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "AB config JSON is empty.";
                return false;
            }

            object deserialized;
            try
            {
                deserialized = SpineLite.MiniJson.Parse(json);
            }
            catch (Exception ex)
            {
                error = "AB config JSON is invalid: " + ex.Message;
                return false;
            }

            if (!(deserialized is Dictionary<string, object> root))
            {
                error = "AB config must contain one top-level JSON object.";
                return false;
            }

            var parser = new Parser();
            config = parser.ParseConfig(root);
            if (parser.Errors.Count == 0 && config != null) return true;

            error = string.Join("; ", parser.Errors);
            config = null;
            return false;
        }

        sealed class Parser
        {
            readonly List<string> _errors = new List<string>();

            public IReadOnlyList<string> Errors => _errors;

            public AbTestConfig ParseConfig(
                Dictionary<string, object> root)
            {
                int versionCode = ReadInt(root, "version_code", "$", true);
                IReadOnlyList<AbTestFlowDomain> flowDomains = ParseArray(
                    root,
                    "flow_domain",
                    "$",
                    ParseFlowDomain);
                IReadOnlyList<AbTestLayer> layers = ParseArray(
                    root,
                    "v6_layer",
                    "$",
                    ParseLayer);

                Dictionary<string, object> rawV5 = ReadObject(
                    root,
                    "v5_config",
                    "$",
                    true);
                AbTestV5Config v5Config = ParseV5Config(
                    rawV5,
                    "$.v5_config");

                Dictionary<string, object> rawDependencies = ReadObject(
                    root,
                    "v6_exp_dependency_map",
                    "$",
                    true);
                IReadOnlyDictionary<string, AbTestJsonValue> dependencies =
                    ParseJsonObject(
                        rawDependencies,
                        "$.v6_exp_dependency_map");

                IReadOnlyList<AbTestExperiment> experiments = ParseArray(
                    root,
                    "v6_exp",
                    "$",
                    ParseExperiment);
                IReadOnlyList<AbTestStrategy> strategies = ParseArray(
                    root,
                    "v6_strategy",
                    "$",
                    ParseStrategy);
                IReadOnlyList<AbTestJsonValue> combinedLayers =
                    ParseJsonArray(root, "combined_layer", "$", true);
                IReadOnlyList<AbTestJsonValue> parameterExcludeRules =
                    ParseJsonArray(root, "param_exclude_rule", "$", true);

                return new AbTestConfig(
                    versionCode,
                    flowDomains,
                    layers,
                    v5Config,
                    dependencies,
                    experiments,
                    strategies,
                    combinedLayers,
                    parameterExcludeRules);
            }

            AbTestFlowDomain ParseFlowDomain(
                Dictionary<string, object> source,
                string path)
            {
                return new AbTestFlowDomain(
                    ReadString(source, "id", path, true),
                    ReadString(source, "diversion_id", path, true),
                    ReadString(source, "domain_tag", path, true),
                    ReadBool(source, "keep_hitting", path, true),
                    ParseStringArray(
                        source,
                        "flow_domain_buckets",
                        path,
                        true),
                    ParseArray(
                        source,
                        "children_domains",
                        path,
                        ParseFlowDomain));
            }

            AbTestLayer ParseLayer(
                Dictionary<string, object> source,
                string path)
            {
                return new AbTestLayer(
                    ReadString(source, "id", path, true),
                    ReadString(source, "flow_domain_id", path, true),
                    ParseStringArray(
                        source,
                        "layer_buckets",
                        path,
                        true));
            }

            AbTestExperiment ParseExperiment(
                Dictionary<string, object> source,
                string path)
            {
                Dictionary<string, object> condition = ReadObject(
                    source,
                    "exp_condition",
                    path,
                    true);
                Dictionary<string, object> extraInfo = ReadObject(
                    source,
                    "extra_info",
                    path,
                    true);
                return new AbTestExperiment(
                    ReadString(source, "id", path, true),
                    ReadString(source, "layer_id", path, true),
                    ReadString(source, "exp_tag", path, true),
                    ReadString(source, "exp_tag_time", path, true),
                    ReadBool(
                        source,
                        "is_empty_running_exp",
                        path,
                        true),
                    ReadBool(
                        source,
                        "retain_experiment_hit",
                        path,
                        true),
                    ParseStringArray(
                        source,
                        "exp_buckets",
                        path,
                        true),
                    ParseCondition(condition, path + ".exp_condition"),
                    ParseArray(
                        source,
                        "exp_group",
                        path,
                        ParseExperimentGroup),
                    ParseJsonObject(extraInfo, path + ".extra_info"));
            }

            AbTestExperimentGroup ParseExperimentGroup(
                Dictionary<string, object> source,
                string path)
            {
                Dictionary<string, object> rawGroupData = ReadObject(
                    source,
                    "group_data",
                    path,
                    true);
                var groupData = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                if (rawGroupData != null)
                {
                    foreach (KeyValuePair<string, object> pair in rawGroupData)
                    {
                        if (pair.Value is string text)
                        {
                            groupData[pair.Key] = text;
                        }
                        else
                        {
                            AddTypeError(
                                path + ".group_data." + pair.Key,
                                "string",
                                pair.Value);
                        }
                    }
                }

                return new AbTestExperimentGroup(
                    ReadString(source, "group_tag", path, true),
                    ReadInt(source, "group_index", path, true),
                    ReadBool(source, "is_closed", path, true),
                    ParseStringArray(
                        source,
                        "history_tag_list",
                        path,
                        true),
                    new ReadOnlyDictionary<string, string>(groupData));
            }

            AbTestCondition ParseCondition(
                Dictionary<string, object> source,
                string path)
            {
                if (source == null)
                {
                    return new AbTestCondition(
                        string.Empty,
                        Array.Empty<AbTestConditionGroup>());
                }

                return new AbTestCondition(
                    ReadString(
                        source,
                        "condition_group_type",
                        path,
                        true),
                    ParseArray(
                        source,
                        "condition_groups",
                        path,
                        ParseConditionGroup));
            }

            AbTestConditionGroup ParseConditionGroup(
                Dictionary<string, object> source,
                string path)
            {
                return new AbTestConditionGroup(
                    ReadString(source, "condition_type", path, true),
                    ParseArray(
                        source,
                        "condition_rules",
                        path,
                        ParseConditionRule));
            }

            AbTestConditionRule ParseConditionRule(
                Dictionary<string, object> source,
                string path)
            {
                IList rawValues = ReadArray(source, "value", path, true);
                var values = new List<AbTestConditionValue>();
                if (rawValues != null)
                {
                    for (int i = 0; i < rawValues.Count; i++)
                    {
                        string valuePath = path + ".value[" + i + "]";
                        AbTestJsonValue jsonValue = ParseJsonValue(
                            rawValues[i],
                            valuePath);
                        AbTestUserTagRule userTagRule =
                            rawValues[i] is Dictionary<string, object> item &&
                            (item.ContainsKey("tag_id") ||
                             item.ContainsKey("tag_values"))
                                ? ParseUserTagRule(item, valuePath)
                                : null;
                        values.Add(new AbTestConditionValue(
                            jsonValue,
                            userTagRule));
                    }
                }

                return new AbTestConditionRule(
                    ReadString(source, "key", path, true),
                    ReadString(source, "value_type", path, true),
                    ReadString(source, "rule_type", path, true),
                    ReadString(source, "op_type", path, true),
                    values.ToArray());
            }

            AbTestUserTagRule ParseUserTagRule(
                Dictionary<string, object> source,
                string path)
            {
                return new AbTestUserTagRule(
                    ReadString(source, "value_type", path, true),
                    ReadString(source, "rule_type", path, true),
                    ReadString(source, "op_type", path, true),
                    ReadString(source, "tag_id", path, true),
                    ParseStringArray(
                        source,
                        "tag_values",
                        path,
                        true),
                    ReadBool(source, "is_full_match", path, true),
                    ReadBool(source, "is_list", path, true));
            }

            AbTestStrategy ParseStrategy(
                Dictionary<string, object> source,
                string path)
            {
                Dictionary<string, object> condition = ReadObject(
                    source,
                    "condition",
                    path,
                    true);
                Dictionary<string, object> extraInfo = ReadObject(
                    source,
                    "extra_info",
                    path,
                    true);
                return new AbTestStrategy(
                    ReadString(source, "flow_domain_id", path, true),
                    ReadString(source, "param_key", path, true),
                    ReadString(source, "param_value", path, true),
                    ReadString(source, "strategy_tag", path, true),
                    ReadInt(source, "priority", path, true),
                    ParseCondition(condition, path + ".condition"),
                    ParseJsonObject(extraInfo, path + ".extra_info"));
            }

            AbTestV5Config ParseV5Config(
                Dictionary<string, object> source,
                string path)
            {
                if (source == null)
                {
                    return new AbTestV5Config(
                        0,
                        Array.Empty<AbTestJsonValue>(),
                        Array.Empty<AbTestJsonValue>(),
                        Array.Empty<AbTestJsonValue>());
                }

                return new AbTestV5Config(
                    ReadInt(source, "version_code", path, true),
                    ParseJsonArray(source, "layers", path, true),
                    ParseJsonArray(source, "params", path, true),
                    ParseJsonArray(
                        source,
                        "flow_domain_params",
                        path,
                        true));
            }

            IReadOnlyList<T> ParseArray<T>(
                Dictionary<string, object> source,
                string key,
                string path,
                Func<Dictionary<string, object>, string, T> parser)
            {
                IList raw = ReadArray(source, key, path, true);
                if (raw == null) return Array.Empty<T>();

                var result = new List<T>(raw.Count);
                for (int i = 0; i < raw.Count; i++)
                {
                    string itemPath = path + "." + key + "[" + i + "]";
                    if (raw[i] is Dictionary<string, object> item)
                    {
                        result.Add(parser(item, itemPath));
                    }
                    else
                    {
                        AddTypeError(itemPath, "object", raw[i]);
                    }
                }

                return result.ToArray();
            }

            IReadOnlyList<string> ParseStringArray(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                IList raw = ReadArray(source, key, path, required);
                if (raw == null) return Array.Empty<string>();

                var result = new List<string>(raw.Count);
                for (int i = 0; i < raw.Count; i++)
                {
                    if (raw[i] is string text)
                    {
                        result.Add(text);
                    }
                    else
                    {
                        AddTypeError(
                            path + "." + key + "[" + i + "]",
                            "string",
                            raw[i]);
                    }
                }

                return result.ToArray();
            }

            IReadOnlyList<AbTestJsonValue> ParseJsonArray(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                IList raw = ReadArray(source, key, path, required);
                if (raw == null) return Array.Empty<AbTestJsonValue>();

                var values = new AbTestJsonValue[raw.Count];
                for (int i = 0; i < raw.Count; i++)
                {
                    values[i] = ParseJsonValue(
                        raw[i],
                        path + "." + key + "[" + i + "]");
                }

                return values;
            }

            IReadOnlyDictionary<string, AbTestJsonValue> ParseJsonObject(
                Dictionary<string, object> source,
                string path)
            {
                var result = new Dictionary<string, AbTestJsonValue>(
                    StringComparer.Ordinal);
                if (source != null)
                {
                    foreach (KeyValuePair<string, object> pair in source)
                    {
                        result[pair.Key] = ParseJsonValue(
                            pair.Value,
                            path + "." + pair.Key);
                    }
                }

                return new ReadOnlyDictionary<string, AbTestJsonValue>(
                    result);
            }

            AbTestJsonValue ParseJsonValue(object value, string path)
            {
                if (value == null)
                {
                    return new AbTestJsonValue(
                        AbTestJsonValueKind.Null,
                        null,
                        0d,
                        false,
                        null,
                        null);
                }

                if (value is string text)
                {
                    return new AbTestJsonValue(
                        AbTestJsonValueKind.String,
                        text,
                        0d,
                        false,
                        null,
                        null);
                }

                if (value is bool boolean)
                {
                    return new AbTestJsonValue(
                        AbTestJsonValueKind.Boolean,
                        null,
                        0d,
                        boolean,
                        null,
                        null);
                }

                if (IsNumber(value))
                {
                    return new AbTestJsonValue(
                        AbTestJsonValueKind.Number,
                        null,
                        Convert.ToDouble(value, CultureInfo.InvariantCulture),
                        false,
                        null,
                        null);
                }

                if (value is Dictionary<string, object> rawObject)
                {
                    return new AbTestJsonValue(
                        AbTestJsonValueKind.Object,
                        null,
                        0d,
                        false,
                        ParseJsonObject(rawObject, path),
                        null);
                }

                if (value is IList rawArray)
                {
                    var result = new AbTestJsonValue[rawArray.Count];
                    for (int i = 0; i < rawArray.Count; i++)
                    {
                        result[i] = ParseJsonValue(
                            rawArray[i],
                            path + "[" + i + "]");
                    }

                    return new AbTestJsonValue(
                        AbTestJsonValueKind.Array,
                        null,
                        0d,
                        false,
                        null,
                        result);
                }

                AddTypeError(path, "JSON value", value);
                return new AbTestJsonValue(
                    AbTestJsonValueKind.Null,
                    null,
                    0d,
                    false,
                    null,
                    null);
            }

            Dictionary<string, object> ReadObject(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                if (!TryRead(source, key, path, required, out object value))
                    return null;
                if (value is Dictionary<string, object> result) return result;
                AddTypeError(path + "." + key, "object", value);
                return null;
            }

            IList ReadArray(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                if (!TryRead(source, key, path, required, out object value))
                    return null;
                if (value is IList result) return result;
                AddTypeError(path + "." + key, "array", value);
                return null;
            }

            string ReadString(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                if (!TryRead(source, key, path, required, out object value))
                    return string.Empty;
                if (value is string result) return result;
                AddTypeError(path + "." + key, "string", value);
                return string.Empty;
            }

            bool ReadBool(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                if (!TryRead(source, key, path, required, out object value))
                    return false;
                if (value is bool result) return result;
                AddTypeError(path + "." + key, "boolean", value);
                return false;
            }

            int ReadInt(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required)
            {
                if (!TryRead(source, key, path, required, out object value))
                    return 0;
                if (IsInteger(value))
                {
                    try
                    {
                        return Convert.ToInt32(
                            value,
                            CultureInfo.InvariantCulture);
                    }
                    catch (Exception)
                    {
                        _errors.Add(
                            path + "." + key +
                            " is outside the Int32 range.");
                        return 0;
                    }
                }

                AddTypeError(path + "." + key, "integer", value);
                return 0;
            }

            bool TryRead(
                Dictionary<string, object> source,
                string key,
                string path,
                bool required,
                out object value)
            {
                value = null;
                if (source != null && source.TryGetValue(key, out value))
                    return true;
                if (required)
                    _errors.Add(path + " is missing required key '" + key + "'.");
                return false;
            }

            void AddTypeError(
                string path,
                string expected,
                object actual)
            {
                _errors.Add(
                    path + " must be " + expected + "; found " +
                    (actual == null
                        ? "null"
                        : actual.GetType().Name) + ".");
            }

            static bool IsInteger(object value)
            {
                return value is sbyte || value is byte ||
                       value is short || value is ushort ||
                       value is int || value is uint ||
                       value is long || value is ulong;
            }

            static bool IsNumber(object value)
            {
                return IsInteger(value) || value is float ||
                       value is double || value is decimal;
            }
        }
    }
}
