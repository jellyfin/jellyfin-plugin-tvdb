/**Setup Event Listeners */
document.addEventListener('viewshow', viewShowEvent)

// Mutation Observer
const observer = new MutationObserver(injectDisplayOrderOptions);
// Only observe when on tv.html, details, home.html, or search.html
observer.disconnect();

function viewShowEvent(){
    const location = window.location.hash;
    console.debug("Current: " + location);
    if (location.startsWith('#/tv.html') || location.startsWith('#/details') || location.startsWith('#/home.html') || location.startsWith('#/search.html')){
        console.debug('Connecting Observer');
        observer.observe(document.body, {childList: true, subtree: true});
    }
    else{
        console.debug('Disconnecting Observer');
        observer.disconnect();
    }
}

function injectDisplayOrderOptions(MutationList, observer){
    console.debug("Mutation Observer Triggered");
    const selectDisplayOrder = document.getElementById('selectDisplayOrder');

    if (!selectDisplayOrder){
        console.debug('selectDisplayOrder not found');
        return;
    }
    console.debug('selectDisplayOrder found');
    console.debug('Injecting Options')
    observer.disconnect();
    const options = [
        {value: 'alternate', text: 'Alternate'},
        {value: 'regional', text: 'Regional'},
        {value: 'altdvd', text: 'Alternate DVD'}
    ]
    //check if options already exist
    if (selectDisplayOrder.options.length > 8){
        console.debug('Options already exist');
        observer.observe(document.body, {childList: true, subtree: true});
        return;
    }
    options.forEach(option => {
        const optionElement = document.createElement('option');
        optionElement.value = option.value;
        optionElement.text = option.text;
        selectDisplayOrder.appendChild(optionElement);
    });
    observer.observe(document.body, {childList: true, subtree: true});
}
