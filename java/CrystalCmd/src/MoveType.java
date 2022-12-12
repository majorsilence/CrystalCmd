import com.google.gson.annotations.SerializedName;

public enum MoveType {
	@SerializedName("1")
	ABSOLUTE(1),

	@SerializedName("2")
	RELATIVE(2);

	private final int levelCode;

	MoveType(int levelCode) {
		this.levelCode = levelCode;
	}
}