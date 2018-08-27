import * as moment           from 'moment';

export default function(
	startDate: moment.Moment, 
	endDate: moment.Moment,
	formatStr: string = 'YYYY-MM-DD'
) {

	var curDate = moment(startDate);
	var dates = [];

	do {
		dates.push(curDate.format(formatStr));

	} while(endDate.isSameOrAfter(curDate.add(1, 'days'), 'day'));

	return dates;
}