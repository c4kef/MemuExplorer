const io = require("socket.io").listen(3000);
const wppconnect = require('../JServer/wppconnect-master/dist');//require('@wppconnect-team/wppconnect');
const sessions = [];
const fs = require('fs');
const QRCode = require('qrcode');

require('events').EventEmitter.defaultMaxListeners = 0;
wppconnect
                    .create({
                        session: "test", //Pass the name of the client you want to start the bot
                        catchQR: (qrCode, asciiQR, attempt, urlCode) => {
                            QRCode.toDataURL(urlCode, function (err, url) {
                                console.log(url);
                                console.log("-----------------------------------------------------------------------");
                                const matches = url.match(/^data:([A-Za-z-+\/]+);base64,(.+)$/),
                                    response = {};

                                if (matches.length !== 3) {
                                    backdata.status = 500;
                                    socket.emit("data", JSON.stringify(backdata));
                                    return;
                                }

                                console.log(matches[2]);
                                response.type = matches[1];
                                response.data = new Buffer.from(matches[2], 'base64');
                            });
                        },
                        headless: false, // Headless chrome
                        devtools: false, // Open devtools by default
                        useChrome: true, // If false will use Chromium instance
                        debug: false, // Opens a debug session
                        logQR: false, // Logs QR automatically in terminal
                        browserWS: '', // If u want to use browserWSEndpoint
                        puppeteerOptions: {}, // Will be passed to puppeteer.launch
                        disableWelcome: true, // Option to disable the welcoming message which appears in the beginning
                        updatesLog: true, // Logs info updates automatically in terminal
                        whatsappVersion: '2.2230.15',
                        autoClose: 25000, // Automatically closes the wppconnect only when scanning the QR code (default 60 seconds, if you want to turn it off, assign 0 or false)
                        tokenStore: 'file', // Define how work with tokens, that can be a custom interface
                    })
                    .then((client) => {
                    })
                    .catch((erro) => {
                    });
io.configure('development', function () {
    io.set('log level', 1);
});

io.sockets.on("connection", function (socket) {
    //console.log("Detect new connection");
    socket.on("data", async function (ndata) {

        const data = JSON.parse(ndata);
        const backdata = {
            status: 0,
            value: []
        };

        backdata.value.push(data["Values"][0].split('@')[1]);

        switch (data["Type"]) {
            case "create":
                //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"create\"");
                removeSession(data["Values"][0].split('@')[0], data["Values"][1].toString() + "\\" + data["Values"][0].split('@')[0]);
                await wppconnect
                    .create({
                        session: data["Values"][0].split('@')[0], //Pass the name of the client you want to start the bot
                        catchQR: (qrCode, asciiQR, attempt, urlCode) => {
                            QRCode.toDataURL(urlCode, function (err, url) {
                                const matches = url.match(/^data:([A-Za-z-+\/]+);base64,(.+)$/),
                                    response = {};

                                if (matches.length !== 3) {
                                    backdata.status = 500;
                                    socket.emit("data", JSON.stringify(backdata));
                                    return;
                                }

                                response.type = matches[1];
                                response.data = new Buffer.from(matches[2], 'base64');

                                fs.writeFile(
                                    'qrs/' + data["Values"][0].split('@')[1].toString() + '.png',
                                    response['data'],
                                    'binary',
                                    function (err) {
                                        if (err != null) {
                                            //backdata.status = 500;
                                            //socket.emit("data", JSON.stringify(backdata));
                                            //console.log("I fucked his mom: " + err);
                                            //return null;
                                        }
                                    });
                            });
                        },
                        headless: false, // Headless chrome
                        devtools: false, // Open devtools by default
                        useChrome: true, // If false will use Chromium instance
                        waitForLogin: (data["Values"][2].toString().toLowerCase() === 'true'),
                        debug: false, // Opens a debug session
                        logQR: false, // Logs QR automatically in terminal
                        browserWS: '', // If u want to use browserWSEndpoint
                        browserArgs: ['--disable-site-isolation-trials', '--renderer-process-limit=2', '--enable-low-end-device-mode'], // Parameters to be added into the chrome browser instance
                        puppeteerOptions: {}, // Will be passed to puppeteer.launch
                        disableWelcome: true, // Option to disable the welcoming message which appears in the beginning
                        updatesLog: true, // Logs info updates automatically in terminal
                        whatsappVersion: '2.2230.15',
                        autoClose: 25000, // Automatically closes the wppconnect only when scanning the QR code (default 60 seconds, if you want to turn it off, assign 0 or false)
                        tokenStore: 'file', // Define how work with tokens, that can be a custom interface
                        folderNameToken: data["Values"][1].toString(), //folder name when saving tokens
                    })
                    .then((client) => {
                        client.onStateChange((state) => {
                            const backdataSub = {
                                status: "statusSession",
                                value: []
                            };
                            backdataSub.value.push(state);
                            socket.emit("state", JSON.stringify(backdataSub));
                        });
                        client.waitForQrCodeScan((qr) =>{
                            const backdataSub = {
                                status: "statusSession",
                                value: []
                            };
                            backdataSub.value.push("qrCodeLoaded");
                            socket.emit("state", JSON.stringify(backdataSub));
                        })
                        //console.log("[" + data["Values"][0].split('@')[0] + "] - PID " + client.waPage.browser().process().pid);
                        //console.log("[" + data["Values"][0].split('@')[0] + "] - Path " + data["Values"][1].toString() + "\\" + data["Values"][0].split('@')[0]);
                        //console.log("[" + data["Values"][0].split('@')[0] + "] - \"create\" sucessful created");
                        backdata.status = 200;
                        sessions.push({
                            name: data["Values"][0].split('@')[0],
                            value: client,
                            path: data["Values"][1].toString() + "\\" + data["Values"][0].split('@')[0]
                        });
                        socket.emit("data", JSON.stringify(backdata));
                    })
                    .catch((erro) => {
                        backdata.status = 500;
                        backdata.value.push(erro);
                        //console.log("[" + data["Values"][0].split('@')[0] + "] - \"create\" not created with errors");
                        socket.emit("data", JSON.stringify(backdata));
                    });
                break;

            case "logout":
                //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"logout\"");
                backdata.status = 200
                await getSession(data["Values"][0].split('@')[0]).logout();
                removeSession(data["Values"][0].split('@')[0], (data["Values"][1].toString().toLowerCase() === 'true'));
                socket.emit("data", JSON.stringify(backdata));
                break;

            case "joinGroup":
                //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"joinGroup\"");
                await getSession(data["Values"][0].split('@')[0])
                    .joinGroup(data["Values"][1])
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

            case "waitForInChat":
                //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"waitForInChat\"");
                backdata.status = 200;
                await getSession(data["Values"][0].split('@')[0]).waitForInChat();
                backdata.value.push(true);
                socket.emit("data", JSON.stringify(backdata));
                break;

            case "free":
                backdata.status = 200
                //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"free\"");
                removeSession(data["Values"][0].split('@')[0], (data["Values"][1].toString().toLowerCase() === 'true'));
                socket.emit("data", JSON.stringify(backdata));
                break;

            case "checkValidPhone":
                //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"checkValidPhone\"");
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
                if (data["Values"].length === 4) {
                    //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"sendText\" with image to " + data["Values"][1].toString());
                    await getSession(data["Values"][0].split('@')[0])
                        .sendImage(data["Values"][1], data["Values"][2], undefined, data["Values"][3])
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
                } else {
                    //console.log("[" + data["Values"][0].split('@')[0] + "] - called function \"sendText\" to " + data["Values"][1].toString());
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
                }
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

function removeSession(name, removeDir) {
    const index = sessions.findIndex(session => session.name === name);

    if (index === -1) {
        if (fs.existsSync(removeDir) && !fs.existsSync(removeDir + ".data.json")) {
            fs.rmSync(removeDir, { recursive: true, force: true });
            //console.log("[" + name + "] - cache removed");
        }
        return;
    }

    const browserPID = sessions[index].value.waPage.browser().process().pid
    process.kill(browserPID);
    //sessions[index].value.close();
    //console.log("[" + name + "] - Close PID " + browserPID);
    if (removeDir === true && fs.existsSync(sessions[index].path)) {
        if (!fs.existsSync(sessions[index].path + ".data.json")) {
            fs.rmSync(sessions[index].path, { recursive: true, force: true, maxRetries: 5, retryDelay: 2000 });
            //console.log("[" + name + "] - cache removed");
        }
        else {
            //console.log("[" + name + "] - we cannot delete the cache, there is an authorization file");
        }
    }

    sessions.splice(index, 1);
}