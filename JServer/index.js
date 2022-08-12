const io = require("socket.io").listen(3000);
const wppconnect = require('@wppconnect-team/wppconnect');
const sessions = [];
const QRCode = require('qrcode');

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
               console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"create\"");
               wppconnect
                   .create({
                    session: data["Values"][0].split('@')[0], //Pass the name of the client you want to start the bot
                    catchQR: (qrCode, asciiQR, attempt, urlCode) => 
                    {
                        QRCode.toDataURL(urlCode, function (err, url) {
                           const matches = url.match(/^data:([A-Za-z-+\/]+);base64,(.+)$/), response = {};

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
                                       //backdata.status = 500;
                                       //socket.emit("data", JSON.stringify(backdata));
                                       console.log("I fucked his mom: " + err);
                                       //return null;
                                   }
                               });
                            });
                    },
                    statusFind: (statusSession, session) => {
                      //console.log('Status Session: ', statusSession); //return isLogged || notLogged || browserClose || qrReadSuccess || qrReadFail || autocloseCalled || desconnectedMobile || deleteToken
                      //Create session wss return "serverClose" case server for close
                      //console.log('Session name: ', session);
                    },
                    headless: false, // Headless chrome
                    devtools: false, // Open devtools by default
                    useChrome: true, // If false will use Chromium instance
                    debug: false, // Opens a debug session
                    logQR: false, // Logs QR automatically in terminal
                    browserWS: '', // If u want to use browserWSEndpoint
                    browserArgs: [''], // Parameters to be added into the chrome browser instance
                    puppeteerOptions: {}, // Will be passed to puppeteer.launch
                    disableWelcome: true, // Option to disable the welcoming message which appears in the beginning
                    updatesLog: true, // Logs info updates automatically in terminal
                    autoClose: 25000, // Automatically closes the wppconnect only when scanning the QR code (default 60 seconds, if you want to turn it off, assign 0 or false)
                    tokenStore: 'file', // Define how work with tokens, that can be a custom interface
                    folderNameToken: './tokens', //folder name when saving tokens
                  })
                   .then((client) => {
                       console.log("[" + data["Values"][0].split('@')[0] + "] - \"create\" sucessful created");
                       backdata.status = 200
                       sessions.push({name: data["Values"][0].split('@')[0], value: client});
                       socket.emit("data", JSON.stringify(backdata));
                   })
                   .catch((erro) => {
                       backdata.status = 500;
                       backdata.value.push(erro);
                       console.log("[" + data["Values"][0].split('@')[0] + "] - \"create\" not created with errors");
                       socket.emit("data", JSON.stringify(backdata));
                   });
               break;

           case "startEvents":
            console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"startEvents\"");
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
               console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"logout\"");
               backdata.status = 200
               await getSession(data["Values"][0].split('@')[0]).logout();
               removeSession(data["Values"][0].split('@')[0]);
               socket.emit("data", JSON.stringify(backdata));
               break;

            case "free":
                backdata.status = 200
                console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"free\"");
                removeSession(data["Values"][0].split('@')[0]);
                socket.emit("data", JSON.stringify(backdata));
                break;
            
            case "checkValidPhone":
                console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"checkValidPhone\"");
                await getSession(data["Values"][0].split('@')[0])
                .checkNumberStatus(data["Values"][1])
                .then((result) => {
                    backdata.status = 200
                    backdata.value.push(result["numberExists"]);
                    socket.emit("data", JSON.stringify(backdata));
                })
                .catch((erro) => {
                    backdata.status = 500
                    backdata.value.push(erro);
                    socket.emit("data", JSON.stringify(backdata));
                });
                break;
               
           case "sendText":
               console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"sendText\"");
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
    
    if (index === -1)
        return;
    
    sessions[index].value.close();
    sessions.splice(index, 1);
}