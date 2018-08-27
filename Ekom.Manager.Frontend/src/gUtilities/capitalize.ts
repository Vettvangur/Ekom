export default function capitalize(str : string) {

    if (str.length > 2) {

        return str[0].toUpperCase() + str.substring(1);
    }

    return str.toUpperCase();
};