public enum MoveType {
	ABSOLUTE(1), RELATIVE(2);

	private final int levelCode;

	private MoveType(int levelCode) {
		this.levelCode = levelCode;
	}
}