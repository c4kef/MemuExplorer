const io = require("socket.io").listen(3000);
const venom = require('venom-bot');

io.configure('development', function()
{
   io.set('log level', 1);
});


io.sockets.on("connection", function (socket) {
   socket.on("data", function (data) {
       
       console.log(data);
       if (data === "start")
       {
           venom
               .create(
                   'sessionName', //session
                   null, //catchQR
                   null, //statusFind
                   null, //options
                   null, //BrowserSessionToken
                   (browser, waPage) => {
                       // Show broser process ID
                       console.log('Browser PID:', browser.process().pid);
                       // Take screenshot before logged-in
                       waPage.screenshot({ path: 'before-screenshot.png' });
                   }
               )
               .then((client) => start(client))
               .catch((erro) => {
                   console.log(erro);
               });
       }
       
      if (data) {
         socket.emit("data", data.toUpperCase(), {length: data.length });
      }
   });
});

function start(client) {
   // Taks screenshot after logged-in
   client.waPage.screenshot({ path: 'after-screenshot.png' });
}