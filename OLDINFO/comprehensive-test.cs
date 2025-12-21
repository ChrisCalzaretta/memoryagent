using System;
using Microsoft.Extensions.Caching.Memory;
using Polly;

// CQRS Pattern
public interface ICommandHandler { }
public interface IQueryHandler { }

// Event Sourcing Pattern  
public class EventStore {
    public void Append(DomainEvent evt) { }
}

// Circuit Breaker Pattern
public class ServiceClient {
    private AsyncCircuitBreakerPolicy _circuitBreaker;
}

// Bulkhead Pattern
public class BulkheadService {
    private AsyncBulkheadPolicy _bulkhead;
}

// Saga Pattern
public class OrderSaga {
    public async Task ProcessAsync() { }
}

// Compensating Transaction
public class CompensateOrder {
    public async Task RollbackAsync() { }
}

// Ambassador Pattern
public class HttpAmbassador {
    private HttpClient _client;
}

// Gateway Aggregation
public class ApiGateway {
    public async Task<Result> AggregateAsync() {
        var result1 = await _httpClient.GetAsync("service1");
        var result2 = await _httpClient.GetAsync("service2");
        return Combine(result1, result2);
    }
}
