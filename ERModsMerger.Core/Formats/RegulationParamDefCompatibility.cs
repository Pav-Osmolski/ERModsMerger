using SoulsFormats;
using System.Collections.Generic;
using System.Linq;

namespace ERModsMerger.Core.Formats
{
    /// <summary>
    /// Applies regulation-version-aware ParamDefs while allowing historical PARAM data-version
    /// metadata to differ only when the physical row layout still matches exactly.
    /// </summary>
    internal static class RegulationParamDefCompatibility
    {
        public static bool TryApply(
            PARAM param,
            PARAMDEF versionAwareDef,
            ulong regulationVersion,
            out bool toleratedDataVersionMismatch)
        {
            toleratedDataVersionMismatch = false;

            PARAMDEF effectiveDef = versionAwareDef.VersionAware
                ? versionAwareDef.GetFilteredParamdefForRegulationVersion(regulationVersion)
                : versionAwareDef;

            if (param.ApplyParamdefCarefully(effectiveDef))
                return true;

            if (!string.Equals(param.ParamType, effectiveDef.ParamType, System.StringComparison.Ordinal))
                return false;

            if (param.DetectedSize != -1 && param.DetectedSize != effectiveDef.GetRowSize())
                return false;

            if (param.ParamdefDataVersion == effectiveDef.DataVersion)
                return false;

            PARAMDEF historicalDataVersionDef = CloneWithDataVersion(
                effectiveDef,
                param.ParamdefDataVersion);

            if (!param.ApplyParamdefCarefully(historicalDataVersionDef))
                return false;

            toleratedDataVersionMismatch = true;
            return true;
        }

        private static PARAMDEF CloneWithDataVersion(PARAMDEF source, short dataVersion)
        {
            return new PARAMDEF
            {
                DataVersion = dataVersion,
                ParamType = source.ParamType,
                BigEndian = source.BigEndian,
                Unicode = source.Unicode,
                FormatVersion = source.FormatVersion,
                Fields = new List<PARAMDEF.Field>(source.Fields)
            };
        }
    }
}
