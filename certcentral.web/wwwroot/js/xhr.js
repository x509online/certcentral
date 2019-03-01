function Get(url, callback, key) {
    var xhr = new XMLHttpRequest();
    
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4 && xhr.status === 200)
            callback(xhr.responseText);
    };
    xhr.open("GET", url);
    if (key) {
        xhr.setRequestHeader("ApiKey", key);
    }
    xhr.send(null);
}