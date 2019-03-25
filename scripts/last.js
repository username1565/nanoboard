
function loadRootThread(hash) {
  var first = hash;
  var prev = hash;
  var fn = function(){
    $.get('../api/get/' + hash)
      .done(function(p){
        p = JSON.parse(p);
        if (p.replyTo == _categories)
        {
          _depth = 2;
          loadThread(prev, first);
          return;
        }
        prev = p.hash;
        hash = p.replyTo;
        fn();
      });
  };
  fn();
}

function loadRootThreadHash(hash,pp,cb) {
  $.post('../api/find-thread/' + hash, _categories)
    .done(function(res){
      cb(res,pp);
    })
    .fail(function(){
      cb(null,pp);
    });
/*
  var first = hash;
  var prev = hash;
  var fn = function(){
    $.get('../api/get/' + hash)
      .done(function(p){
        p = JSON.parse(p);
        if (p.replyTo == _categories)
        {
          cb(prev,pp);
          return;
        }
        prev = p.hash;
        hash = p.replyTo;
        fn();
      });
  };
  fn();
*/
}

function showLast(N){
    $.get('../api/pcount')
      .done(function(cnt){
        cnt = parseInt(cnt);
        $.get('../api/prange/'+Math.max(cnt-N,0)+'-'+N)
          .done(function(arr){
            active_tab("lastli")
            arr = JSON.parse(arr);
/*
//3.1
			if(document.getElementById('createPNG').style.display === 'block'){
				var threadId = 'thread';
			}else{
				var threadId = 'queue_thread';
			}
*/			
            if (arr.length > 0) {
//3.0
              $('#thread').empty();
//3.1
//              $('#'+threadId).empty();
            } else { return; }
            for (var i = arr.length - 1; i >= 0; i--) {
//3.0
              var p = addPost(arr[i], function(d) { d.appendTo($('#thread')); }, false);
//3.1
//              var p = addPost(arr[i], function(d) { d.appendTo($('#'+threadId)); }, false);
              if (arr[i].hash != _categories && 
                  arr[i].replyTo != _categories && 
                  arr[i].replyTo != _rootpost &&
                  p
                  ) {
                loadRootThreadHash(p.attr('id'), p,
                  function(h,pp) {
                  pp.append(
                    $('<a>')
//3.0
                      .attr('href', '#thread' + h)
//3.1
//                      .attr('href', '#'+threadId + h)
                      .html('<span class="glyphicon glyphicon-menu-hamburger" aria-hidden="true"></span><span class="btn-title">&thinsp;'+(h == null ? 'Thread Not Found' : 'Thread')+'</span>')
                      .click(function(){
                        //loadRootThread($(this).parent().attr('id'));
                      })
                    );
                  });
              }
            }
            vid_show()
          });
      });
  }
