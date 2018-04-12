
import java.io.IOException;
import java.io.InputStream;

import org.apache.commons.fileupload.RequestContext;

import com.sun.net.httpserver.HttpExchange;

public class ExchangeRequestContext implements RequestContext
{
	private final HttpExchange exchange;

	public ExchangeRequestContext(HttpExchange exchange)
	{
		this.exchange = exchange;
	}

	@Override
	public String getCharacterEncoding()
	{
		return null;
	}

	@Override
	public int getContentLength()
	{
		String contentLength = exchange.getRequestHeaders().getFirst("Content-Length");
		if( contentLength != null && contentLength.length() > 0 )
		{
			try
			{
				return Integer.parseInt(contentLength);
			}
			catch( NumberFormatException n )
			{
				
			}
		}
		return -1;
	}

	@Override
	public String getContentType()
	{
		return exchange.getRequestHeaders().getFirst("Content-Type");	
	}

	@Override
	public InputStream getInputStream() throws IOException
	{
		return exchange.getRequestBody();
	}
}