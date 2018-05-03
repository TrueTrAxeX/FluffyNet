# FluffyNet
Lightweight Network Library C#

Библиотека использует AES-256 шифрование для передачи сообщений и RSA-2048 для совершения рукопожатия. Все пакеты сериализуются в бинарный формат BSON. Протокол подходит для обмена как мелкими, так и большими сообщениями.

Для начала на серверной и клиентской части создадим классы-пакеты.

```csharp
// Пакет, который будет посылать клиент
public class MyRequestPacket : Packet
{
    [JsonProperty(PropertyName = "msg")]
    public string Message { get; set; }
    
    public MyRequestPacket()
    {
        Id = (int) 1;
    }
}

// Пакет, которым будет отвечать сервер
public class MyResponsePacket : Packet
{
    [JsonProperty(PropertyName = "msg")]
    public string Message { get; set; };

    public MyResponsePacket()
    {
        Id = (int) 2;
    }
}
```

Пример серверной части

```csharp
FluffyServer server = new FluffyServer(
                port: 42444, // Порт на котором будет слушать соединения
                maxWaitPingTime: TimeSpan.FromSeconds(45), // Максимальное время ожидания пинга от клиента, если пинг не поступил, то соединение будет разорвано 
                messageEncryptionEnabled: true, // Включение шифрования сообщений с помощью алгоритмов AES 256 + RSA 2048
                maxReceivePacketLength: 2400000); // Максимальное количество байт, которое может принять сервер, иначе он разрывает соединение

server.StartAsync(); // Запускает сервер

// Создаем контейнер для хранения данных клиента
server.StoreInit += client =>
{
    client.Store = new YourDataClass(); // Здесь должен быть люблой ваш класс, в котором вы будете хранить данные
}

// В этом событии будут обрабатывать все пакеты пришедшие от клиента
server.NewPacket += (ref int id, FluffyNet.Server.PacketParser<Packet> parser, FluffyClient client) =>
{
    switch (id)
    {
        case 1:
            // Если ID пакета совпадает, то запускаем парсер пакета по его типу
            MyRequestPacket request = (MyRequestPacket) parser.Invoke(typeof(MyRequestPacket));

            // Отправляем пакет клиенту, первым аргументом указав запрос, который пришел от клиента
            // Можно также использовать метод client.SendPacket, если у вас нет "пакета-запроса" от клиента
            client.SendResponse(request, new MyResponsePacket() {Message = "You are welcome!"});
        break;
    }
};

 // Подключился новый клиент
server.NewClientConnected += client => { };

// Клиент отключился
server.ClientDisconnected += client => { };
```

Пример клиентской части:

```csharp
// Создаем экземпляр клиента и указываем IP адрес и порт к которому хотим подключиться
FluffyNetClient client = new FluffyNetClient("127.0.0.1", 42444);

// Подключаемся к серверу
if(client.Connect()) 
{
  // Обрабатываем событие отключения от сервера
  cl.Disconnected += () => { Console.WriteLine("Были отключены от сервера"); }

  // Обрабатываем новые пакаеты пришедшие от сервера
  cl.NewPacket += (ref int id, FluffyNet.Client.PacketParser<Packet> parser, FluffyNetClient client) =>
  {
    switch (id)
    {
        case 2:
            // Пакет придедший от сервера
            MyResponsePacket response = (MyResponsePacket) parser.Invoke(typeof(MyResponsePacket));

            Console.WriteLine("Сообщение от сервера: " + response.Message);
        break;
    }
  };
  
  // Отправим тестовый запрос серверу
  cl.SendPacket(new MyRequestPacket() { Message = "Hello world!"; });
  
  // Если вы хотите отправить запрос и сразу получить ответи, используйте метод SendPacketAndGetResponse
  var response = cl.SendPacketAndGetResponse<MyRequestPacket, MyResponsePacket>(new MyRequestPacket() {Message = "Hello world!"}, 
  timeout: TimeSpan.FromSeconds(5)); // Поставим максимальное время ожидания пакета в 5 секунд, если пакет не пришел, то метод вернет null

}
```
