(function($){

	var api = {
		register: function(options) {
			$(options.wrapper).on('tap', 'div.conversation', function(e){
				var n = $(this);
				if (n.hasClass('unread')) {
					$.telligent.evolution.put({
						url: $.telligent.evolution.site.getBaseUrl() + 'api.ashx/v2/conversations/{ConversationId}/read.json',
						data: {
							ConversationId: n.data('conversationid'),
							HasRead: 'True'
						},
						success: function(r) {
							n.removeClass('unread');
							jQuery.telligent.evolution.messaging.publish('notification.read', {
								typeId: options.notificationTypeId,
								unreadCount: '-=1'
							});
						}
					});
					// force a reload of this page on next view to indicate read state
					$.telligent.evolution.mobile.clearContent();
				}
			});

			$.telligent.evolution.mobile.scrollable({
				region: 'content',
				load: function(pageIndex, success, error) {
					if ($(options.wrapper + ' div.data:last').data('hasmore')) {
						$.telligent.evolution.get({
							url: options.moreUrl,
							data: {
								w_pageIndex: pageIndex
							},
							success: function(r) {
								success(r);
							},
							error: function() {
								error();
							}
						});
					} else {
						success();
					}
				},
				complete: function(content) {
					content = $(content).css({ opacity: 0 });
					$(options.wrapper).find('div.post-list').append(content);
					content.evolutionTransform({ opacity: 1 }, { duration: 400 });
				}
			});

			$.telligent.evolution.messaging.subscribe('notification.raised', function(d) {
				if (d.typeId != options.notificationTypeId) {
					$.telligent.evolution.mobile.refresh();
				}
			});

			var newLink = $('<a href="' + options.newConversationUrl + '" class="add">' + options.newConversationLabel + '</a>');
			$.telligent.evolution.mobile.setHeaderButton(newLink);
		}
	}

	$.telligent = $.telligent || {};
	$.telligent.evolution = $.telligent.evolution || {};
	$.telligent.evolution.widgets = $.telligent.evolution.widgets || {};
	$.telligent.evolution.widgets.conversationList = api;

})(jQuery);