using DotBase.Event;
using DotBase.Log;
using LibRTIC.MiniTaskLib.Model;
using System.Collections.ObjectModel;

namespace LibRTIC.MiniTaskLib;

/// <summary>
/// All events in _collection are defined by C# generics. They need to be enabled first using
/// <see cref="EnableInvokeFor"/> before <see cref="Invoke"/> of an event handler will work.
/// </summary>
public class EventProducerCollection : IDisposable
{
#if DEBUG_UNDISPOSED
    public static int UNDISPOSED_COUNT = 0;

    public static int INSTANCE_COUNT = 0;

    private bool _disposed = false;
#endif

    public string Label = "";

    public bool IsComplete { get { return _complete; } }

    private object _lock = new object();

    private bool _complete = false;

    private Collection<IDisposable> _eventConnections = new();

    private Collection<IEventContainerInstance> _collection = new();

    public EventProducerCollection(InfoLog info, string label)
    {
        this.Label = label;
#if DEBUG_UNDISPOSED
        Interlocked.Increment(ref UNDISPOSED_COUNT);
        Interlocked.Increment(ref INSTANCE_COUNT);
#endif
    }

#if DEBUG_UNDISPOSED
    ~EventProducerCollection()
    {
        Dispose(false);
        Interlocked.Decrement(ref INSTANCE_COUNT);
    }
#endif

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
#if DEBUG_UNDISPOSED
        if (!_disposed && !disposing)
        {
            throw new InvalidOperationException("Not disposed properly.");
        }
        if (!_disposed)
        {
            _disposed = true;
            Interlocked.Decrement(ref UNDISPOSED_COUNT);
        }
#endif

        // Release managed resources.
        if (disposing)
        {
            Clear();
        }
        // Release unmanaged resources.
    }

    public virtual void Clear()
    {
        lock (_lock)
        {
            _complete = true;
            foreach (IDisposable connection in _eventConnections)
            {
                connection.Dispose();
            }
            _eventConnections.Clear();
            _collection.Clear();
        }
    }

    /// <summary>
    /// For 'Invoke' and 'Connect' to work for specific type of object (TMessage), it is first necessary to enable them.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <exception cref="ArgumentException"></exception>
    public EventContainer<TMessage>? EnableInvokeFor<TMessage>(bool assureIsNewEvent = false)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventProducerCollection.");
#else
                return null;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() == 0)
            {
                var item = new EventContainer<TMessage>();
                _collection.Add(item);
                return item;
            }
            else if (assureIsNewEvent)
            {
                throw new ArgumentException("Attempted to add pre-existing event type into EventProducerCollection.");
            }
            else
            {
                return items.First();
            }
        }
    }

    public bool Exists<TMessage>()
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventProducerCollection.");
#else
                return false;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            return items.Count() > 0;
        }
    }

    public void Invoke<TMessage>(TMessage update, object? sender = null)
    {
        var item = GetEventContainer<TMessage>();
        item?.Invoke(sender, update);
    }

    public EventContainer<TMessage>? GetEventContainer<TMessage>()
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventProducerCollection.");
#else
                return null;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventProducerCollection.");
            }
            else if (items.Count() == 1)
            {
                return items.First();
            }

#if DEBUG
            throw new ArgumentException("Attempted to access non-existent event type in EventProducerCollection.");
#else
            return null;
#endif
        }
    }

    public EventContainer<TMessage>? ForwardFrom<TMessage>(EventProducerCollection sourceEvents, IEventMailboxWriter destinationMailbox)
    {
        if (!Exists<TMessage>())
        {
            EnableInvokeFor<TMessage>();
        }

        if (!sourceEvents.Exists<TMessage>())
        {
            sourceEvents.EnableInvokeFor<TMessage>();
        }

        var item = GetEventContainer<TMessage>();
        if (item is not null)
        {
            EventHandler<TMessage> forwarder = (_, message) =>
            {
                if (!destinationMailbox.IsWriterComplete)
                {
                    destinationMailbox.Post(() => item.Invoke(null, message));
                }
            };

            return sourceEvents.ConnectForwarder<TMessage>(forwarder);
        }

        return null;
    }

    public void Connect<TMessage>(EventHandler<TMessage> eventHandler)
    {
        Connect(false, eventHandler);
    }

    public void Connect<TMessage>(bool assertEventExists, EventHandler<TMessage> eventHandler)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventProducerCollection.");
#else
                return;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventProducerCollection.");
            }
            else if (items.Count() == 1)
            {
                ConnectEventHandler(items.First(), eventHandler);
            }
            else
            {
                if (assertEventExists)
                {
                    throw new ArgumentException("Attempted to access non-existent event type in EventProducerCollection.");
                }
                else
                {
                    var item = EnableInvokeFor<TMessage>();
                    if (item is not null)
                    {
                        ConnectEventHandler(item, eventHandler);
                    }
                }
            }
        }
    }

    public void ConnectAsync<TMessage>(EventHandler<TMessage> eventHandler)
    {
        ConnectAsync(true, eventHandler);
    }

    public void ConnectAsync<TMessage>(bool assertEventExists, EventHandler<TMessage> eventHandler)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventProducerCollection.");
#else
                return;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventProducerCollection.");
            }
            else if (items.Count() == 1)
            {
                ConnectEventHandlerAsync(items.First(), eventHandler);
            }
            else
            {
                if (assertEventExists)
                {
                    throw new ArgumentException("Attempted to access non-existent event type in EventProducerCollection.");
                }
                else
                {
                    var item = EnableInvokeFor<TMessage>();
                    if (item is not null)
                    {
                        ConnectEventHandlerAsync(item, eventHandler);
                    }
                }
            }
        }
    }

    private EventContainer<TMessage> ConnectForwarder<TMessage>(EventHandler<TMessage> forwarder)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventProducerCollection.");
#else
                return null;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventProducerCollection.");
            }
            else if (items.Count() == 1)
            {
                var item = items.First();
                ConnectMailboxForwarder(item, forwarder);
                return item;
            }
            else
            {
                throw new ArgumentException("Attempted to access non-existent event type in EventProducerCollection.");
            }
        }
    }

    private void ConnectEventHandlerAsync<TMessage>(EventContainer<TMessage> item, EventHandler<TMessage> eventHandler)
    {
        item.AddHandlerAsync(eventHandler);
        _eventConnections.Add(new AsyncHandlerConnection<TMessage>(item, eventHandler));
    }

    private void ConnectEventHandler<TMessage>(EventContainer<TMessage> item, EventHandler<TMessage> eventHandler)
    {
        item.AddHandler(eventHandler);
        _eventConnections.Add(new EventHandlerConnection<TMessage>(item, eventHandler));
    }

    private void ConnectMailboxForwarder<TMessage>(EventContainer<TMessage> item, EventHandler<TMessage> eventForwarder)
    {
        item.AddHandler(eventForwarder);
        _eventConnections.Add(new EventHandlerConnection<TMessage>(item, eventForwarder));
    }

    private void ConnectEventHandler<TMessage>(EventContainer<TMessage> item, EventContainer<TMessage> eventHandler)
    {
        item.SendTo(eventHandler);
        _eventConnections.Add(new EventForwardingConnection<TMessage>(item, eventHandler));
    }
}
