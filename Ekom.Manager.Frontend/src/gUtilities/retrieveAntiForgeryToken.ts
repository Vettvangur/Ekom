export default function retrieveAntiForgeryToken() {

    var name = '__RequestVerificationToken';
    var el = document.getElementsByName(name)[0] as HTMLInputElement;

    return name + '=' + el.value;
}