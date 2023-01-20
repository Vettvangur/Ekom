export default function icelandTimeParse(dateString : string) {

    return new Date(dateString.split('.')
                              .reverse()
                              .join('.'));
};