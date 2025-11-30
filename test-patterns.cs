public class CQRSExample {
    public interface ICommandHandler { }
    public interface IQueryHandler { }
    
    public class CircuitBreaker {
        public async Task ExecuteAsync() { }
    }
    
    public class EventStore {
        public void Append(DomainEvent evt) { }
    }
}
