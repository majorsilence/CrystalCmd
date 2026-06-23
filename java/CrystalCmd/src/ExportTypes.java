import com.google.gson.annotations.SerializedName;

/**
 * Mirrors Majorsilence.CrystalCmd.Common.ExportTypes (same order/ordinals).
 * Accepts both the enum name ("PDF") and the numeric value ("4") on the wire so
 * payloads from the C# client (Newtonsoft serialises enums as integers) and from
 * name-based serialisers both deserialise correctly.
 */
public enum ExportTypes {
    @SerializedName(value = "CSV", alternate = {"0"}) CSV,
    @SerializedName(value = "CrystalReport", alternate = {"1"}) CrystalReport,
    @SerializedName(value = "Excel", alternate = {"2"}) Excel,
    @SerializedName(value = "ExcelDataOnly", alternate = {"3"}) ExcelDataOnly,
    @SerializedName(value = "PDF", alternate = {"4"}) PDF,
    @SerializedName(value = "RichText", alternate = {"5"}) RichText,
    @SerializedName(value = "TEXT", alternate = {"6"}) TEXT,
    @SerializedName(value = "WordDoc", alternate = {"7"}) WordDoc
}
