// Fallback function for copying text to clipboard when the modern API is not available
window.fallbackCopyToClipboard = function (text) {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    
    // Avoid scrolling to bottom
    textArea.style.top = "0";
    textArea.style.left = "0";
    textArea.style.position = "fixed";
    
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    
    try {
        const successful = document.execCommand('copy');
        if (successful) {
            // Could add a toast notification here if desired
            console.log('Text copied to clipboard (fallback method)');
        }
    } catch (err) {
        console.error('Fallback: Could not copy text to clipboard', err);
    }
    
    document.body.removeChild(textArea);
};