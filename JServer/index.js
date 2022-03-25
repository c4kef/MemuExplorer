const io = require("socket.io").listen(3000);
const wppconnect = require('@wppconnect-team/wppconnect');
const sessions = [];

io.configure('development', function()
{
   io.set('log level', 1);
});

io.sockets.on("connection", function (socket)
{
   socket.on("data", async function (ndata) {

       const data = JSON.parse(ndata);
       const backdata = {
           status: 0,
           value: []
       };

       backdata.value.push(data["Values"][0].split('@')[1]);

       switch (data["Type"]) {
           case "create":
               wppconnect
                   .create(
                       data["Values"][0].split('@')[0],
                       (qrCode, asciiQR, attempt, urlCode) => {
                           console.log(qrCode);
                           console.log(asciiQR);
                           console.log(attempt);
                           console.log(urlCode);
                           const matches = qrCode.match(/^data:([A-Za-z-+\/]+);base64,(.+)$/), response = {};

                           if (matches.length !== 3) {
                               backdata.status = 500;
                               socket.emit("data", JSON.stringify(backdata));
                               return;
                           }

                           response.type = matches[1];
                           response.data = new Buffer.from(matches[2], 'base64');

                           require('fs').writeFile(
                               'qrs/' + data["Values"][0].split('@')[1].toString() + '.png',
                               response['data'],
                               'binary',
                               function (err) {
                                   if (err != null) {
                                       backdata.status = 500;
                                       socket.emit("data", JSON.stringify(backdata));
                                       console.log(err);
                                       return null;
                                   }
                               });
                       },
                       null,
                       {
                           headless: false, // Headless chrome
                           devtools: false, // Open devtools by default
                           useChrome: true, // If false will use Chromium instance
                           debug: false, // Opens a debug session
                           logQR: false, // Logs QR automatically in terminal
                           disableWelcome: false, // Will disable Spinnies animation, useful for containers (docker) for a better log
                           updatesLog: false, // Logs info updates automatically in terminal
                       },
                       async (WABrowserId, WAToken1, WAToken2, WASecretBundle) => {
                           console.log('Browser PID:', WABrowserId);
                       }
                   )
                   .then((client) => {
                       backdata.status = 200
                       sessions.push({name: data["Values"][0].split('@')[0], value: client});
                       socket.emit("data", JSON.stringify(backdata));
                   })
                   .catch((erro) => {
                       backdata.status = 500;
                       backdata.value.push(erro);
                       socket.emit("data", JSON.stringify(backdata));
                   });
               break;

           case "startEvents":
               backdata.status = 200

               await getSession(data["Values"][0].split('@')[0]).onMessage(message => {
                   const backdata = {
                       value: message
                   };

                   socket.emit("event", JSON.stringify(backdata));
               })

               socket.emit("data", JSON.stringify(backdata));
               break;

           case "logout":
               backdata.status = 200
               await getSession(data["Values"][0].split('@')[0]).logout();
               removeSession(data["Values"][0].split('@')[0]);
               socket.emit("data", JSON.stringify(backdata));
               break;
               
           case "sendText":
               await getSession(data["Values"][0].split('@')[0])
                   .sendText(data["Values"][1], data["Values"][2])
                   .then((result) => {
                       backdata.status = 200
                       backdata.value.push(result);
                       socket.emit("data", JSON.stringify(backdata));
                   })
                   .catch((erro) => {
                       backdata.status = 500
                       backdata.value.push(erro);
                       socket.emit("data", JSON.stringify(backdata));
                   });
               break;

           default:
               backdata.status = 404;
               backdata.value.push("operation not found");
               socket.emit("data", JSON.stringify(backdata));
               break;
       }
   });
});

function getSession(name) {
    const index = sessions.findIndex(session => session.name === name);
    return (index === -1) ? null : sessions[index].value;
}

function removeSession(name) {
    const index = sessions.findIndex(session => session.name === name);
    sessions.splice(index, 1);
}