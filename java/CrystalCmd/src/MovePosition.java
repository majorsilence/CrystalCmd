import com.google.gson.annotations.SerializedName;

public enum MovePosition {

	@SerializedName("1")
	TOP(1),

	@SerializedName("2")
	LEFT(2);

	private final int levelCode;

	MovePosition(int levelCode) {
		this.levelCode = levelCode;
	}


}