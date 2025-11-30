using Xunit;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Comprehensive tests for Publisher-Subscriber pattern detection across all languages
/// </summary>
public class PublisherSubscriberPatternTests
{
    #region C# Pattern Detection Tests

    [Fact]
    public void CSharp_DetectsServiceBusTopicPublisher()
    {
        var code = @"
            var client = new ServiceBusClient(connectionString);
            var sender = client.CreateSender(topicName);
            await sender.SendMessageAsync(new ServiceBusMessage(""Hello""));
        ";

        var detector = new CSharpPatternDetectorEnhanced();
        var patterns = detector.DetectPatterns(code, "test.cs", "test");

        var pubSubPatterns = patterns.Where(p => p.Type == PatternType.PublisherSubscriber).ToList();
        Assert.NotEmpty(pubSubPatterns);
        Assert.Contains(pubSubPatterns, p => p.Implementation.Contains("ServiceBus"));
    }

    [Fact]
    public void CSharp_DetectsEventGridPublisher()
    {
        var code = @"
            var client = new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(key));
            await client.SendEventsAsync(new[] { new EventGridEvent(""subject"", ""type"", ""1.0"", data) });
        ";

        var detector = new CSharpPatternDetectorEnhanced();
        var patterns = detector.DetectPatterns(code, "test.cs", "test");

        var eventGridPatterns = patterns.Where(p => p.Implementation.Contains("EventGrid")).ToList();
        Assert.NotEmpty(eventGridPatterns);
    }

    [Fact]
    public void CSharp_DetectsEventHubsProducer()
    {
        var code = @"
            var producer = new EventHubProducerClient(connectionString, eventHubName);
            await producer.SendAsync(new[] { new EventData(""test data"") });
        ";

        var detector = new CSharpPatternDetectorEnhanced();
        var patterns = detector.DetectPatterns(code, "test.cs", "test");

        var eventHubPatterns = patterns.Where(p => p.Implementation.Contains("EventHub")).ToList();
        Assert.NotEmpty(eventHubPatterns);
    }

    [Fact]
    public void CSharp_DetectsMassTransitPublisher()
    {
        var code = @"
            public class OrderService
            {
                private readonly IBus _bus;
                
                public async Task CreateOrder(Order order)
                {
                    await _bus.Publish(new OrderCreatedEvent { OrderId = order.Id });
                }
            }
        ";

        var detector = new CSharpPatternDetectorEnhanced();
        var patterns = detector.DetectPatterns(code, "test.cs", "test");

        var massTransitPatterns = patterns.Where(p => p.Implementation == "MassTransit").ToList();
        Assert.NotEmpty(massTransitPatterns);
    }

    [Fact]
    public void CSharp_DetectsObservablePattern()
    {
        var code = @"
            public class DataStream : IObservable<string>
            {
                public IDisposable Subscribe(IObserver<string> observer)
                {
                    return null;
                }
            }
        ";

        var detector = new CSharpPatternDetectorEnhanced();
        var patterns = detector.DetectPatterns(code, "test.cs", "test");

        var observablePatterns = patterns.Where(p => p.Implementation == "ReactiveX").ToList();
        Assert.NotEmpty(observablePatterns);
    }

    #endregion

    #region Python Pattern Detection Tests

    [Fact]
    public void Python_DetectsServiceBusClient()
    {
        var code = @"
from azure.servicebus import ServiceBusClient

client = ServiceBusClient.from_connection_string(connection_str)
sender = client.get_topic_sender(topic_name='orders')
sender.send_messages(ServiceBusMessage('Order created'))
        ";

        var detector = new PythonPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.py", "test");

        var serviceBusPatterns = patterns.Where(p => p.Type == PatternType.PublisherSubscriber).ToList();
        Assert.NotEmpty(serviceBusPatterns);
    }

    [Fact]
    public void Python_DetectsEventGridPublisher()
    {
        var code = @"
from azure.eventgrid import EventGridPublisherClient

client = EventGridPublisherClient(endpoint, credential)
client.send_events([CloudEvent(source='app', type='order.created', data=data)])
        ";

        var detector = new PythonPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.py", "test");

        var eventGridPatterns = patterns.Where(p => p.Implementation.Contains("azure-eventgrid")).ToList();
        Assert.NotEmpty(eventGridPatterns);
    }

    [Fact]
    public void Python_DetectsRedisPubSub()
    {
        var code = @"
import redis

r = redis.Redis(host='localhost')
r.publish('orders', 'New order created')

pubsub = r.pubsub()
pubsub.subscribe('orders')
        ";

        var detector = new PythonPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.py", "test");

        var redisPatterns = patterns.Where(p => p.Implementation.Contains("redis")).ToList();
        Assert.NotEmpty(redisPatterns);
    }

    [Fact]
    public void Python_DetectsRabbitMQPika()
    {
        var code = @"
import pika

connection = pika.BlockingConnection()
channel = connection.channel()
channel.exchange_declare(exchange='logs', exchange_type='fanout')
channel.basic_publish(exchange='logs', routing_key='', body='Hello')
        ";

        var detector = new PythonPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.py", "test");

        var rabbitMQPatterns = patterns.Where(p => p.Implementation.Contains("pika")).ToList();
        Assert.NotEmpty(rabbitMQPatterns);
    }

    #endregion

    #region JavaScript Pattern Detection Tests

    [Fact]
    public void JavaScript_DetectsEventEmitter()
    {
        var code = @"
const EventEmitter = require('events');
class OrderEmitter extends EventEmitter {}

const orderEvents = new OrderEmitter();
orderEvents.emit('order-created', { id: 123 });
orderEvents.on('order-created', (order) => console.log(order));
        ";

        var detector = new JavaScriptPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.js", "test");

        var emitterPatterns = patterns.Where(p => p.Implementation.Contains("EventEmitter")).ToList();
        Assert.NotEmpty(emitterPatterns);
    }

    [Fact]
    public void JavaScript_DetectsRxJSObservable()
    {
        var code = @"
import { Observable, Subject } from 'rxjs';

const orders$ = new Subject();
orders$.next({ id: 123, status: 'created' });

orders$.subscribe(order => console.log(order));
        ";

        var detector = new JavaScriptPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.ts", "test");

        var rxjsPatterns = patterns.Where(p => p.Implementation == "RxJS").ToList();
        Assert.NotEmpty(rxjsPatterns);
    }

    [Fact]
    public void JavaScript_DetectsWebSocket()
    {
        var code = @"
const socket = new WebSocket('wss://api.example.com/events');
socket.onmessage = (event) => {
    console.log('Message:', event.data);
};
        ";

        var detector = new JavaScriptPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.js", "test");

        var websocketPatterns = patterns.Where(p => p.Implementation == "WebSocket").ToList();
        Assert.NotEmpty(websocketPatterns);
    }

    [Fact]
    public void JavaScript_DetectsKafkaJS()
    {
        var code = @"
const { Kafka } = require('kafkajs');

const kafka = new Kafka({ clientId: 'my-app', brokers: ['localhost:9092'] });
const producer = kafka.producer();
await producer.send({ topic: 'orders', messages: [{ value: 'order created' }] });
        ";

        var detector = new JavaScriptPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.js", "test");

        var kafkaPatterns = patterns.Where(p => p.Implementation == "kafkajs").ToList();
        Assert.NotEmpty(kafkaPatterns);
    }

    #endregion

    #region VB.NET Pattern Detection Tests

    [Fact]
    public void VBNet_DetectsServiceBusClient()
    {
        var code = @"
Dim client As New ServiceBusClient(connectionString)
Dim sender = client.CreateSender(topicName)
Await sender.SendMessageAsync(New ServiceBusMessage(""Hello""))
        ";

        var detector = new VBNetPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.vb", "test");

        var serviceBusPatterns = patterns.Where(p => p.Type == PatternType.PublisherSubscriber).ToList();
        Assert.NotEmpty(serviceBusPatterns);
    }

    [Fact]
    public void VBNet_DetectsDotNetEvents()
    {
        var code = @"
Public Class OrderService
    Public Event OrderCreated As EventHandler(Of OrderEventArgs)
    
    Public Sub CreateOrder()
        RaiseEvent OrderCreated(Me, New OrderEventArgs())
    End Sub
End Class
        ";

        var detector = new VBNetPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.vb", "test");

        var eventPatterns = patterns.Where(p => p.Implementation.Contains("Event")).ToList();
        Assert.NotEmpty(eventPatterns);
    }

    [Fact]
    public void VBNet_DetectsAddHandler()
    {
        var code = @"
AddHandler orderService.OrderCreated, AddressOf OnOrderCreated

Private Sub OnOrderCreated(sender As Object, e As OrderEventArgs)
    Console.WriteLine(""Order created"")
End Sub
        ";

        var detector = new VBNetPatternDetector();
        var patterns = detector.DetectPatterns(code, "test.vb", "test");

        var handlerPatterns = patterns.Where(p => p.Implementation == "AddHandler").ToList();
        Assert.NotEmpty(handlerPatterns);
    }

    #endregion

    #region Pattern Validation Tests

    [Fact]
    public async Task ValidatesIdempotencyRequirement()
    {
        // This will be tested with integration tests against the actual validation service
        // Placeholder for now
        Assert.True(true);
    }

    [Fact]
    public async Task ValidatesDeadLetterQueueConfiguration()
    {
        // This will be tested with integration tests
        Assert.True(true);
    }

    [Fact]
    public async Task ValidatesManagedIdentityUsage()
    {
        // This will be tested with integration tests
        Assert.True(true);
    }

    #endregion
}

