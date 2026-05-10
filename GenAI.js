const API_KEY = "AIzaSyCnL8UGfQG0b77o1OJ0jF4VLshP9sBIYGk";

function toggleChat() {
    let box = document.getElementById("chatBox");
    box.style.display = box.style.display === "block" ? "none" : "block";
}

document.addEventListener("keydown", function (e) {
    if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        send();
    }
});

async function send() {

    let txt = document.getElementById("txt");
    let msg = txt.value.trim();
    if (!msg) return;

    let messages = document.getElementById("messages");

    // User message
    messages.innerHTML += `<div class="user">${escape(msg)}</div>`;
    txt.value = "";

    // Loading
    let loadingId = "loading_" + Date.now();
    messages.innerHTML += `<div class="ai" id="${loadingId}">Thinking...</div>`;
    scrollBottom();

    try {

        const res = await fetch(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-goog-api-key": API_KEY
                },
                body: JSON.stringify({
                    contents: [
                        {
                            parts: [
                                { text: msg }
                            ]
                        }
                    ]
                })
            }
        );

        const data = await res.json();
        document.getElementById(loadingId).remove();
        if (res.status === 429) {

            //throw new Error(
            //    "Rate limit exceeded. Please wait a minute.");

            messages.innerHTML += `<div class="ai">"Rate limit exceeded. Please wait a minute."</div>`;

        }
        else {



            let reply =
                data ?.candidates ?.[0] ?.content ?.parts ?.[0] ?.text
                    || "No response";

            messages.innerHTML += `<div class="ai">${escape(reply)}</div>`;
        }

        scrollBottom();

    } catch (err) {

        document.getElementById(loadingId).remove();

        messages.innerHTML += `<div class="ai">Error calling Gemini API</div>`;
    }
}

function scrollBottom() {
    let m = document.getElementById("messages");
    m.scrollTop = m.scrollHeight;
}

function escape(text) {
    let div = document.createElement("div");
    div.innerText = text;
    return div.innerHTML;
}
