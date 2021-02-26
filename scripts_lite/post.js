function shortenHash(hash) {
  return hash.substring(0,4) + '..' + hash.substring(28,32);
}

function replaceAll(text, search, replace) {
  return text.split(search).join(replace);
}

function replaceAll_search(text, search, replace) {
	var result = text;
	if(
			search.indexOf("/")	===	-1	&&
			search.indexOf("[xmg")	===	-1
	){
		var regexp = new RegExp(search, 'gi');
		var match = text.match(regexp);
		if(match!=null && match.length==1 && match[0]!=regexp){
			replace = replace.split(regexp).join(match[0]);
			result = replaceAll(result, regexp, replace);
		}else if(match!=null && match.length>1){
			for(i=0; i<match.length; i++){
				//modify replace to current match
				replace = replace.split(regexp).join(match[i]);
				var t=0;   
				result =
					result.replace(regexp,
						function (match) {
							t++;
							return ((t-1) === i) ? replace : match;
						}
					)
				;
			}
		}
	}else{
		result = text.split(search).join(replace);
	}
  return result;
}

function clear_shit(text){
	return replaceAll(
		text,
		window.location.origin.split(window.location.protocol)[1]+'/scripts/',
		''
	);
	//because some services, like https://nboardn46ay6ycdi.onion.pet
	//replacing links like (href="blahblah") to (href="//nboardn46ay6ycdi.onion.pet/scripts/blahblah...")	
}

function escapeTags(text) {
  return text
    .replace(/>/gim, '&gt;')
    .replace(/</gim, '&lt;');
}

function detectImages(text) {
	var prefix = 'data:image/jpeg;base64,';
	//var matches = text.match(/\[(i|x)mg=[A-Za-z0-9+\/=]{4,64512}\]/g);	//images from karasiq nanoboard don't display as images, with limit.
  
	var regex = /\[(i|x)mg=[A-Za-z0-9+\/=]{4,}\]/g	;
	var result, indexes = [];
	while ( (result = regex.exec(text)) ) {
		indexes.push(result.index);
	}
	//console.log('matches', matches, ', indexes', indexes);					//array with indexes	
	var matches = text.match(regex);										//Now - OK.
	//console.log('matches', matches);
	if (matches != null) {
		for (var i = 0; i < matches.length; i++) {
			var value = matches[i].toString();
			value = value.substring(5);
			value = value.substring(0, value.length - 1);
		
			var isfname = text.substring(indexes[i], text.length);		//get second part of message, before next value
			var isfname = isfname.split("]")[1];							//split by ']\n[filename.ext]' -> ( '' + "]" + '\n[filename.ext' + "]" )
			var isfname = isfname.substring(2, isfname.length);				//substring '\n[' to get filename.ext
			var test = /\.([0-9a-z]+)(?:[\?#]|$)/i.test(isfname);		//test is filename.ext there
			//console.log("detectImages, isfname", isfname, '(isfname==\'\')', (isfname==''), 'test', test);	//show result
	  
			if(isfname!=='' && test==true){		//if not empty string and if [filename.ext] after image
				var file_name = '['+isfname+']';	//add "[" and "]" to 'filename.ext' 
				var download_link = '<a href="'+prefix+value+'" download="'+isfname+'">['+isfname+']</a>';
				text = replaceAll(text, file_name, download_link);	//replace this to link for download the file.
				//console.log('<a href="'+prefix+value+'" download="'+isfname+'">['+isfname+']</a>'); //show this link
			}
	  
			//console.log("value before: "+value, "prefix", prefix);
			value = '<img src="' + prefix + value + '" />';
			//console.log("value after: "+value);
			//console.log("detectImages. text: ", text, "matcher[i]", matches[i], "value", value);
			text = replaceAll(text, matches[i], value);
		}
	}
	//console.log('post.js: applyformatting, detectImages', '- img replaced to tag img');
	
/*
//	console.log("text", text);
	
	//var localtion_href = window.location.href.split('/').splice(0, 3).join('/');
	text = replaceAll(
		text,
		window.location.origin.split(window.location.protocol)[1]+'/scripts/data:',
		'data:'
	);
	//because some services, like https://nboardn46ay6ycdi.onion.pet
	//replacing links like (href="data:image...") to (href="//nboardn46ay6ycdi.onion.pet/scripts/data:image...")
	
//	console.log("text", text);
*/
	text = clear_shit(text);
	return text;
}

function addPlace(place,uuid) {
	alert(	'This is a lite-server. You cann\'t change and see places on full local-server.\n'+
			'You can add this thread to places on full local-server.\n'+
			'Just download and run it.'
	);
	return;
/*	
  place = Base64.decode(place);
  console.log('add:' + place);
  $.get('../api/paramget/places')
    .done(function(arr){
      arr = arr.split('\n');
      var wasAdded = arr.indexOf(place) != -1;
      if (wasAdded) {
			var elements = document.querySelectorAll('[id="'+uuid+'"]');
			for(i=0; i<elements.length; i++){
				$(elements[i]).text('added');	//add added for all elements
			}
//        $(document.getElementById(uuid)).text('added');	//old code
        pushNotification('Was added already.');
        return;
      }
      arr.push(place);
      $.post('../api/paramset/places', arr.join('\n'))
        .done(function(){
			var elements = document.querySelectorAll('[id="'+uuid+'"]');
			for(i=0; i<elements.length; i++){
				$(elements[i]).text('added');	//add added for all elements
			}
//        $(document.getElementById(uuid)).text('added');				//old code.
          pushNotification('Added: ' + place);
			updatePlacesBar_once();
        });
    });
*/
}

function delPlace(place,uuid) {
	alert(	'This is a lite-server. You cann\'t change and see places on full local-server.\n'+
			'You can add this thread to places on full local-server.\n'+
			'Just download and run it.'
	);
	return;
	
/*
  place = Base64.decode(place);
  console.log('del:' + place);
  $.get('../api/paramget/places')
    .done(function(arr){
      arr = arr.split('\n');
      var wasAdded = arr.indexOf(place) != -1;
      if (!wasAdded) {
			var elements = document.querySelectorAll('[id="'+uuid+'"]');	//get elements with the same id
			for(i=0; i<elements.length; i++){
				$(elements[i]).text('');									//remove "added" for all
			}
		//	$(document.getElementById(uuid)).text('');			//old code
        pushNotification('Not present or already deleted.');
        return;
      }
      var arr2 = [];
      for (var i = 0; i < arr.length; i++) {
        if (arr[i] != place) {
          arr2.push(arr[i]);
        }
      }
      arr = arr2;
      $.post('../api/paramset/places', arr.join('\n'))
        .done(function(){
			//console.log('delete: uuid: ', uuid);

			var elements = document.querySelectorAll('[id="'+uuid+'"]');	//get elements with the same id
			for(i=0; i<elements.length; i++){
				$(elements[i]).text('');						//remove "added" for all
			}
			
//          $(document.getElementById(uuid)).text('');		//old code
			pushNotification('Deleted: ' + place);
			updatePlacesBar_once();
        });
    });
	*/
}

function generateUUID(){
    var d = new Date().getTime();
    if(window.performance && typeof window.performance.now === "function"){
        d += performance.now(); //use high-precision timer if available
    }
    var uuid = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        var r = (d + Math.random()*16)%16 | 0;
        d = Math.floor(d/16);
        return (c=='x' ? r : (r&0x3|0x8)).toString(16);
    });
    return uuid;
}

function detectPlacesCommands(obj) {
	return;	//places commands disabled on lite-server, because paramget and paramset api-command is disabled for remote users in DbApiHandler.cs
/*
  var text = obj.text();
  var html = obj.html();
  var incl = ''.includes == undefined ? function(x,y) { return x.contains(y); } : function(x,y) { return x.includes(y); };
  var matches = text.match(/(ADD|DEL(ETE|))[\s]*https?:\/\/[a-z%&\?\-=_\.0-9\/:#]+/g);
  if (matches == null) return;
  $.get('../api/paramget/places')
    .done(function(arr){
      arr = arr.split('\n');
      for (var i = 0; i < matches.length; i++) {
        var value = matches[i].toString();
        //console.log(value);
        value = value.substring(value.indexOf('http'));
        var wasAdded = arr.indexOf(value) != -1;
        
		//var uuid = generateUUID();								//old code using random UUID for each link
		var uuid = Sha256.hash(value);//.substring(0, 8);			//now using sha256 hash there, to add/remove "added" for all links in each post.
		
        html = replaceAll(html, value+'</a>', value +
          '</a>&nbsp;<a href=javascript:addPlace("'+Base64.encode(value)+'","'+uuid+'")><sup>[+]</sup></a>'+
          '<i><sup id="'+uuid+'">' + (wasAdded ? 'added' : '') + '</sup></i>' +
          '<a href=javascript:delPlace("'+Base64.encode(value)+'","'+uuid+'")><sup>[-]</sup></a>');
      }
      obj.html(html);
    });
*/
}

//function to get link from text (+hash)
var get_link = function (value, post){
	return '<a target="_blank" href="'+value+((typeof post!=='undefined')?post:'')+'">'+value+'</a>';
}

function detectURLs(text) {
//	console.log("detectURLs. text before: ", text);
//  var matches = text.match(/https?:\/\/[A-Za-z%&\?\-=_\.0-9\/:#]+/g);	//old code not matching cyrillic symbols. "https://wiki.1chan.ca/Наноборда"

	//New regexp
	var matches = text.match(/(https?|ftps?|mailto|gopher|tox|irc|skype|magnet):?:\/\/(-\.)?[^\s\/?\.#<>]?[А-яЁёA-z0-9%~&()@\?\!\$\'\*\+\,\;\-=_\.\/:#]+/g);
	//cyrillic symbols in url - OK
	//more protocols - OK
	//dot " ", ".", "!", "?", ":", in the end of sentence not skiped. Can be "The link is http://example.org/index.html!!!" -> "http://example.org/index.html"
	//")" not skip when "(" not found in url earlier. Can be: "https://example.com/my(text).html))))))))" -> "https://example.com/my(text).html"
	
	//console.log('post.js - detectURLs(), mathes: ', matches);

  var you_re=new RegExp(".*youtube\.com.*")
  if (matches != null) {
    for (var i = 0; i < matches.length; i++) {
      var value = matches[i].toString();
	  
		//console.log('post.js detectURLs(), before replacement: matches['+i+']', value);
		var addition_string = '';
		if(
				(value.indexOf(')')	!==	-1)			//if closing bracket found in URL
			&&	(value.indexOf('(')	===	-1)			//and if opened bracket not found in URL
		){
			addition_string = value.substring(value.indexOf(')'), value.length);
			value = value.substring(0, value.indexOf(')'));	//leave URL before closed bracket.
		}
		//skip "]" in the end of link.
		if(
				(value.indexOf('[')	!==	-1)			//if closing bracket found in URL
			&&	(value.indexOf(']')	===	-1)			//and if opened bracket not found in URL
		){
			addition_string = value.substring(value.indexOf('['), value.length);
			value = value.substring(0, value.indexOf('['));	//leave URL before closed bracket.
		}
		
		if(
				(value.indexOf('(')	!==	-1)
			&&	(value.indexOf(')')	===	-1)
		){
			addition_string = value.substring(value.indexOf('('), value.length);
			value = value.substring(0, value.indexOf('('));	//leave URL before closed bracket.			
		}
		
		if(value[value.length-1] === '.'){
			addition_string = '.';
			value = value.substring(0, value.length-1);			 //remove dot in the end, if exist
		}
		
		//console.log('post.js detectURLs(), after replacement: matches['+i+']', value);
		//console.log('addition_string', addition_string);
	  
	  
      if (you_re.test(value))
      {
        value ='<a class="vd-vid" href="'+value+'"><span class="glyphicon glyphicon-play" aria-hidden="true"></span>'+value+'</a>';
        text = replaceAll(text, matches[i], value);
      }
      else
      {
		//current element - to link
        //value = '<a target=_blank href="'+value+'">'+value+'</a>';
        //replace in text current element to link
        //text = replaceAll(text, matches[i], value);
				//in this case, substrings replaced in replased strings.
			
			//do not do the double replace...
			//value = matches[i].toString();									//value for finding and replace. (defined earlier)
			var link = get_link(value)+addition_string;		//the link from value to insert
			
/*
			//var localtion_href = window.location.href.split('/').splice(0, 3).join('/');
			link = replaceAll(
				link,
				window.location.origin.split(window.location.protocol)[1]+'/scripts/',
				''
			);
			//because some services, like https://nboardn46ay6ycdi.onion.pet
			//replacing links like (href="blahblah...") to (href="//nboardn46ay6ycdi.onion.pet/scripts/blahblah...")

			//console.log('link:', link);
*/
			link = 	clear_shit(link);
			
			var start_index = 0;												//increment start_index to search substrings, from this start_index
			
			for(j=0; j<matches.length; j++){									//for each url in array
				var next_url_index = text.indexOf(matches[j], start_index);		// search this url in text from start_index.
				if(next_url_index==-1){											//if this this url not found
					continue;													//continue find next url
				}																//else...
																			//continue the code...
				start_index = next_url_index; 									//else, if found, make this as start index.

//				console.log("maybe_href", maybe_href);

/*
				var maybe_href = text.replace('//', '\/\/').substring(start_index-6, start_index);	//copy previous 6 symbols
				
				if(maybe_href.startsWith('//')){
					maybe_href = (window.location.href).split('/')[0] + maybe_href;
					console.log(maybe_href);
				}
*/
				var maybe_href = text.substring(start_index-6, start_index);	//copy previous 6 symbols
//				console.log("maybe_href", maybe_href);
				if(maybe_href==='href="'){
				//if previous 6 symbols === 'href="' - do not replace text_link to link, this already replaced.
					var next_quote = text.indexOf('"', start_index);				//find next quote index.
					var url_length = next_quote-start_index;						//get length of URL
					start_index += url_length*2+6;									//move start_index 'url_length">url_length</a>'.length = url_length*2+6
				}else{															//if not href - then replace.
					var maybe_post = text.substring(start_index+matches[i].length+59, start_index+matches[i].length+59+32);
					if(
							typeof maybe_post !== 'undefined'
						&& 	(/[A-Fa-f0-9]{32}/g.test(maybe_post))
					){
						link = get_link(value, maybe_post)+addition_string;		//using post number inside the link, but leave link text.
					}
					text = 
							text.substring(0, start_index)									//substring by start_index
							+link															//insert link with link to first part
							+text.substring(start_index+matches[i].length, text.length);	//add second part
					break;
				}
			}
      }
    }
  }
	
/*
	text = replaceAll(
		text,
		'<a target="_blank" href="'+window.location.origin.split(window.location.protocol)[1]+'/scripts/',
		''
	);
	//because some services, like https://nboardn46ay6ycdi.onion.pet
	//replacing links like (href="blahblah") to (<a target="_blank" href="//nboardn46ay6ycdi.onion.pet/scripts/<a target="_blank" href="blahblah)
*/	
	//<a target="_blank" href="//nboardn46ay6ycdi.onion.pet/scripts/
//	console.log("detectURLs. text after: ", text);
	
  return text;
}

function detectThreadLinks(text) {
var matches = text.match(/&gt;&gt;[a-f0-9]{32}/g);
  if (matches != null) {
    for (var i = 0; i < matches.length; i++) {
      var value = matches[i].toString();
      value = value.substring(8, value.length);
      value = '<a href="javascript:void(0);" onclick=_depth=2;loadThread("'+value+'") title="Click to open post/thread/category...">&gt;&gt;' + value + '</a>';
      text = replaceAll(text, matches[i], value);
    }
  }
  return text;
}

function applyFormatting(text) {
//	console.log('text', text);
	if(text.trim()===''){	//if empty post, from notif
		return false;			//do not do nothing...
	}
  text = text.replace(/&gt;(.*)/gi, "<gr>&gt;$1</gr>")
  text = text.replace(/\[sign=[a-f0-9]{128}\]/gim, '');
  text = text.replace(/\[pow=[a-f0-9]{256}\]/gim, '');
  text = text.replace(/\[sp(oiler|)\]/gim, '[x]');
  text = text.replace(/\[\/sp(oiler|)\]/gim, '[/x]');
  text = text.replace(/\[quote\]/gim, '<gr>&gt;');
  text = text.replace(/\[\/quote\]/gim, '</gr>');
  text = text.replace(/\[code\]/gim, '<pre style="display: inline-block; border: 0px; padding: 0px; margin: 0px;">');
  text = text.replace(/\[\/code\]/gim, '</pre>');
  text = text.replace(/\[sup\]/gim, '<sup>');
  text = text.replace(/\[\/sup\]/gim, '</sup>');
  text = text.replace(/\[sub\]/gim, '<sub>');
  text = text.replace(/\[\/sub\]/gim, '</sub>');
  text = text.replace(/\[o\]/gim, '<span style=text-decoration:overline>');
  text = text.replace(/\[\/o\]/gim, '</span>');

  var tags = 'biusxg';
  for (var x = 0; x < tags.length; x++) {
    var ch = tags.charAt(x);
    text = text
      .replace(new RegExp("\\[" + ch + "\\]", 'gim'), '<' + ch + '>')
      .replace(new RegExp("\\[/" + ch + "\\]", 'gim'), '</' + ch + '>');
  }
  text = detect_files(text);
//  console.log('text before detectimages: ', text);
  text = detectImages(text);	//here adding some link on https://nboardn46ay6ycdi.onion.pet/pages/index.html
//  console.log('text after detectimages: ', text);
  text = replaceAll(text, 'data:image/jpeg;base64,iVBORw0K', 'data:image/png;base64,iVBORw0K');	//replace to jpeg to PNG if PNG signature found in base64
  text = detectThreadLinks(text);
  text = replaceAll(text, '\n', '<br/>');
  text = replaceAll(text, '  ', '&nbsp; ');
  if (_detectURLs == 'true') text = detectURLs(text);
  
//  console.log('applyFormatting: text', text);
  return text
    .replace(/<x>/gim, '<sp>')
    .replace(/<\/x>/gim, '</sp>');
}

(function($) {
  $.fn.extend({
    addTemporaryClass: function(className, duration) {
      var elements = this;
      setTimeout(function() {
        elements.removeClass(className);
      }, duration);
      return this.each(function() {
        $(this).addClass(className);
      });
    }
  });
})(jQuery);
