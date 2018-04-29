import java.util.List;
import java.util.Map;

public class Data {
        
        private byte[] ReportFile;
        public void setReportFile(byte[] reportFile) {
        	this.ReportFile = reportFile;
        }
        public byte[] getReportFile() {
        	return this.ReportFile;
        }
        
        //Map<String, String> map = new HashMap<String, String>();
        //map.put("dog", "type of animal");      
        private Map<String, Object> Parameters;
        public void setParameters(Map<String, Object> parameters) {
        	this.Parameters = parameters;
        }
        public Map<String, Object> getParameters(){
        	return this.Parameters;
        }
                      
        //List<String> list = new ArrayList<String>;
        private List<MoveObjects> MoveObjectPosition;
        public void setMoveObjectPosition(List<MoveObjects> moveObjectPosition) {
        	this.MoveObjectPosition = moveObjectPosition;
        }
        public List<MoveObjects> getMoveObjectPosition(){
        	return this.MoveObjectPosition;
        }
        
        private Map<String, String> DataTables;
        public void setDataTables(Map<String, String> dataTables) {
        	this.DataTables = dataTables;
        }
        public Map<String, String> getDataTables(){
        	return this.DataTables;
        }
        
        private List<SubReports> SubReportDataTables;
        public void setSubReportDataTables(List<SubReports> dataTables) {
        	this.SubReportDataTables = dataTables;
        }
        public List<SubReports> getSubReportDataTables(){
        	return this.SubReportDataTables;
        }
        
    }
    
    
    

    

    

