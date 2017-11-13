public enum MovePosition {
	TOP(1), LEFT(2);

	private final int levelCode;

	private MovePosition(int levelCode) {
		this.levelCode = levelCode;
	}
}